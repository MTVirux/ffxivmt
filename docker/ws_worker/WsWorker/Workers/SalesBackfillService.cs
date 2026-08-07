using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

using WsWorker.Options;

namespace WsWorker.Workers;

/// <summary>
/// Imports Universalis history into <c>sales</c>. Progress is tracked per catalogue bucket rather
/// than per region: a pass is well over three hundred requests, and requiring every one of them to
/// succeed before advancing meant a single upstream failure pinned the whole region on the same
/// window indefinitely.
/// </summary>
public sealed class SalesBackfillService : BackgroundService
{
    private readonly IBackfillStateStore _stateStore;
    private readonly ISaleStore _saleStore;
    private readonly WorldStructureService _catalog;
    private readonly IDirtyPairQueue _dirtyPairs;
    private readonly UniversalisOptions _uniOptions;
    private readonly BackfillOptions _backfillOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SalesBackfillService> _logger;

    private readonly TokenBucket _rateLimiter;

    private const int StateIdle = 0;
    private const int StateRunning = 1;
    private const int StateError = 3;

    private enum BucketOutcome
    {
        Advanced,
        Stalled,
        Complete,
        Skipped,
    }

    private static void SetLoopState(string region, string loop, int state) =>
        MetricsCatalog.BackfillState.WithLabels(region, loop).Set(state);

    public SalesBackfillService(
        IBackfillStateStore stateStore,
        ISaleStore saleStore,
        WorldStructureService catalog,
        IDirtyPairQueue dirtyPairs,
        IOptions<UniversalisOptions> uniOptions,
        IOptions<BackfillOptions> backfillOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<SalesBackfillService> logger)
    {
        _stateStore = stateStore;
        _saleStore = saleStore;
        _catalog = catalog;
        _dirtyPairs = dirtyPairs;
        _uniOptions = uniOptions.Value;
        _backfillOptions = backfillOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _rateLimiter = new TokenBucket(
            capacity: _uniOptions.MaxRequestsPerSecondBurst,
            refillRate: _uniOptions.MaxRequestsPerSecond);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("SalesBackfillService initialized - starting live-gap and historical crawl loops");

        await Task.WhenAll(
            RunLoop(BackfillLoops.Live, _backfillOptions.LiveGapIntervalMinutes, ct),
            RunLoop(BackfillLoops.Historical, _backfillOptions.HistoricalCrawlIntervalMinutes, ct));
    }

    private async Task RunLoop(string loop, int intervalMinutes, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("Backfill [{Loop}]: starting pass", loop);

            foreach (var region in _uniOptions.RegionsToImport)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    await RunPass(region, loop, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backfill [{Region}/{Loop}]: unhandled error", region, loop);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), ct);
        }
    }

    private async Task RunPass(string region, string loop, CancellationToken ct)
    {
        SetLoopState(region, loop, StateRunning);
        var faulted = false;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tuning = _backfillOptions.Tuning;

            var itemIds = await _catalog.GetMarketableItemIdsAsync(ct);
            var bucketCount = BackfillBuckets.BucketCountFor(itemIds.Count, tuning.ItemsPerRequest);
            var groups = BackfillBuckets.Group(itemIds, bucketCount);

            var states = await LoadOrSeedStates(region, loop, bucketCount, now, ct);

            var stalled = 0;
            var advanced = 0;
            var completed = 0;

            using var semaphore = new SemaphoreSlim(tuning.Concurrency, tuning.Concurrency);

            var tasks = groups.Select(async group =>
            {
                if (!states.TryGetValue(group.Key, out var state) || state.CrawlComplete)
                    return;

                await semaphore.WaitAsync(ct);
                try
                {
                    switch (await RunBucket(region, loop, state, group.Value, now, ct))
                    {
                        case BucketOutcome.Stalled: Interlocked.Increment(ref stalled); break;
                        case BucketOutcome.Advanced: Interlocked.Increment(ref advanced); break;
                        case BucketOutcome.Complete: Interlocked.Increment(ref completed); break;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            MetricsCatalog.BackfillStalledBuckets.WithLabels(region, loop).Set(stalled);
            if (stalled > 0)
                MetricsCatalog.BackfillPointerStalledTotal.WithLabels(region, loop).Inc(stalled);

            _logger.LogInformation(
                "Backfill [{Region}/{Loop}]: {Advanced} advanced, {Stalled} stalled, {Completed} finished their history",
                region, loop, advanced, stalled, completed);

            if (loop == BackfillLoops.Historical)
                await ReportHistoryDepth(region, now, ct);
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            SetLoopState(region, loop, faulted ? StateError : StateIdle);
        }
    }

    private async Task<BucketOutcome> RunBucket(
        string region,
        string loop,
        BackfillBucketState state,
        IReadOnlyList<int> items,
        DateTimeOffset now,
        CancellationToken ct)
    {
        DateTimeOffset windowStart;
        DateTimeOffset? olderThan = null;

        if (loop == BackfillLoops.Historical)
        {
            var earliest = state.EarliestImportAt ?? now;
            windowStart = BackfillWindow.HistoricalStart(earliest, _backfillOptions.ChunkDays);
            olderThan = earliest;
        }
        else
        {
            var last = state.LastImportAt ?? now;
            if (now - last < TimeSpan.FromMinutes(_backfillOptions.SkipIfGapUnderMinutes))
                return BucketOutcome.Skipped;

            windowStart = last;
        }

        var entriesWithinSeconds = BackfillWindow.EntriesWithinSeconds(windowStart, now);
        var tuning = _backfillOptions.Tuning;
        var requestTimeout = tuning.RequestTimeoutFor(entriesWithinSeconds);

        var run = await BackfillChunkRunner.RunAsync(
            [items],
            (chunk, token) => FetchChunk(region, chunk, entriesWithinSeconds, requestTimeout, token),
            tuning.RetryRounds,
            concurrency: 1,
            TimeSpan.FromSeconds(tuning.RetryRoundDelaySeconds),
            _rateLimiter.ConsumeAsync,
            ct);

        // Leaving the pointer put is what makes this bucket retry next pass. It must never be
        // confused with "this bucket has no more history".
        if (run.FailedChunks > 0)
            return BucketOutcome.Stalled;

        var toWrite = olderThan is null
            ? run.Sales
            : run.Sales.Where(s => s.SaleTime < olderThan.Value).ToList();

        if (toWrite.Count > 0)
        {
            await _saleStore.AddBatchAsync(toWrite, ct);
            await EnqueueDirtyPairs(toWrite, ct);
        }

        if (loop == BackfillLoops.Historical)
        {
            if (toWrite.Count == 0)
            {
                await _stateStore.UpsertBucketAsync(state with { CrawlComplete = true }, ct);
                return BucketOutcome.Complete;
            }

            await _stateStore.UpsertBucketAsync(state with { EarliestImportAt = windowStart }, ct);
        }
        else
        {
            await _stateStore.UpsertBucketAsync(state with { LastImportAt = now }, ct);
        }

        return BucketOutcome.Advanced;
    }

    /// <summary>
    /// Buckets advance independently, so the depth guaranteed across the whole catalogue is the
    /// one furthest behind.
    /// </summary>
    private async Task ReportHistoryDepth(string region, DateTimeOffset now, CancellationToken ct)
    {
        var states = await _stateStore.GetBucketsAsync(region, BackfillLoops.Historical, ct);

        var pending = states
            .Where(s => !s.CrawlComplete && s.EarliestImportAt.HasValue)
            .Select(s => s.EarliestImportAt!.Value)
            .ToList();

        if (pending.Count == 0)
            return;

        MetricsCatalog.BackfillHistoryOldestSeconds.WithLabels(region)
            .Set((now - pending.Max()).TotalSeconds);
    }

    private async Task<Dictionary<int, BackfillBucketState>> LoadOrSeedStates(
        string region, string loop, int bucketCount, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _stateStore.GetBucketsAsync(region, loop, ct);
        var byBucket = existing.ToDictionary(s => s.Bucket);

        if (byBucket.Count > 0)
        {
            // The catalogue grows, so later passes can see buckets that were never seeded.
            for (var bucket = 0; bucket < bucketCount; bucket++)
            {
                byBucket.TryAdd(bucket, new BackfillBucketState(region, loop, bucket, now, now, false));
            }

            return byBucket;
        }

        var (legacyLast, legacyEarliest) = await _stateStore.GetLegacyPointersAsync(region, ct);
        var seedLast = legacyLast ?? now;
        var seedEarliest = legacyEarliest ?? now;

        _logger.LogInformation(
            "Backfill [{Region}/{Loop}]: seeding {Count} buckets from the pre-bucket pointers (last={Last:u}, earliest={Earliest:u})",
            region, loop, bucketCount, seedLast, seedEarliest);

        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var seeded = new BackfillBucketState(region, loop, bucket, seedLast, seedEarliest, false);
            byBucket[bucket] = seeded;
            await _stateStore.UpsertBucketAsync(seeded, ct);
        }

        return byBucket;
    }

    private async Task EnqueueDirtyPairs(IReadOnlyList<Sale> sales, CancellationToken ct)
    {
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var pairs = sales
            .Where(s => s.SaleTime > sevenDaysAgo)
            .Select(s => (s.WorldId, s.ItemId))
            .ToHashSet();

        if (pairs.Count > 0)
            await _dirtyPairs.EnqueueManyAsync(pairs, ct);
    }

    /// <summary>Null means the request failed; an empty list means the window genuinely held no sales.</summary>
    private async Task<List<Sale>?> FetchChunk(
        string region,
        IReadOnlyList<int> itemIds,
        long entriesWithinSeconds,
        TimeSpan requestTimeout,
        CancellationToken ct)
    {
        var itemIdStr = string.Join(",", itemIds);
        var url = $"{_uniOptions.BaseUrl.TrimEnd('/')}/history/{region}/{itemIdStr}?entriesWithin={entriesWithinSeconds}&entriesToReturn=99999";

        var client = _httpClientFactory.CreateClient("backfill_universalis");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(requestTimeout);
        var token = timeoutCts.Token;

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, token);
        }
        catch (HttpRequestException ex)
        {
            MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
            _logger.LogWarning("FetchChunk [{Region}] HTTP request failed: {Message}", region, ex.Message);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // This budget spans the Polly retries, not one response, so exhausting it usually means
            // repeated transient 5xx rather than a slow server. Logging it as a plain timeout hid a
            // run of upstream 504s behind what looked like latency.
            MetricsCatalog.BackfillPagesTotal.WithLabels(region, "timeout").Inc();
            _logger.LogWarning(
                "FetchChunk [{Region}] exhausted its {Timeout:F0}s budget for {Count} items - retries of transient 5xx are included in that budget",
                region, requestTimeout.TotalSeconds, itemIds.Count);
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning("FetchChunk [{Region}] returned {StatusCode} for {Count} items",
                    region, (int)response.StatusCode, itemIds.Count);
                return null;
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(token);
            }
            catch (Exception ex)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning(ex, "FetchChunk [{Region}] failed reading response body", region);
                return null;
            }

            try
            {
                var sales = UniversalisHistoryParser.Parse(json);
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "ok").Inc();
                MetricsCatalog.BackfillRowsTotal.WithLabels(region).Inc(sales.Count);
                return sales;
            }
            catch (Exception ex)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning(ex, "FetchChunk [{Region}] failed parsing JSON", region);
                return null;
            }
        }
    }

    private sealed class TokenBucket : IDisposable
    {
        private readonly double _capacity;
        private readonly double _refillRate;
        private double _tokens;
        private long _lastRefillTick;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public TokenBucket(int capacity, int refillRate)
        {
            _capacity = capacity;
            _refillRate = refillRate;
            _tokens = capacity;
            _lastRefillTick = Stopwatch.GetTimestamp();
        }

        public async Task ConsumeAsync(CancellationToken ct)
        {
            while (true)
            {
                await _lock.WaitAsync(ct);
                try
                {
                    Refill();
                    if (_tokens >= 1.0)
                    {
                        _tokens -= 1.0;
                        return;
                    }
                }
                finally
                {
                    _lock.Release();
                }

                var waitMs = (int)(1000.0 / _refillRate);
                await Task.Delay(waitMs, ct);
            }
        }

        private void Refill()
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _lastRefillTick) / (double)Stopwatch.Frequency;
            _tokens = Math.Min(_capacity, _tokens + elapsed * _refillRate);
            _lastRefillTick = now;
        }

        public void Dispose() => _lock.Dispose();
    }

    public override void Dispose()
    {
        _rateLimiter.Dispose();
        base.Dispose();
    }
}
