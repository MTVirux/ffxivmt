using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace WsWorker.Workers;

/// <summary>
/// Imports Universalis history into <c>sales</c>. Progress is per catalogue bucket, not per region:
/// a pass is hundreds of requests, and advancing only when all of them succeed let one upstream
/// failure pin a whole region on the same window indefinitely.
/// </summary>
public sealed class SalesBackfillService : BackgroundService
{
    private readonly IBackfillStateStore _stateStore;
    private readonly ISaleWriter _saleWriter;
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

    private sealed record BucketWork(
        BackfillBucketState State,
        IReadOnlyList<int> Items,
        BackfillBucketWindow Window);

    private static void SetLoopState(string region, string loop, int state) =>
        MetricsCatalog.BackfillState.WithLabels(region, loop).Set(state);

    public SalesBackfillService(
        IBackfillStateStore stateStore,
        ISaleWriter saleWriter,
        WorldStructureService catalog,
        IDirtyPairQueue dirtyPairs,
        IOptions<UniversalisOptions> uniOptions,
        IOptions<BackfillOptions> backfillOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<SalesBackfillService> logger)
    {
        _stateStore = stateStore;
        _saleWriter = saleWriter;
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
            RunLoop(BackfillLoopSpec.Live, _backfillOptions.LiveGapIntervalMinutes, ct),
            RunLoop(BackfillLoopSpec.Historical, _backfillOptions.HistoricalCrawlIntervalMinutes, ct));
    }

    private async Task RunLoop(BackfillLoopSpec loop, int intervalMinutes, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("Backfill [{Loop}]: starting pass", loop.Name);

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
                    _logger.LogWarning(ex, "Backfill [{Region}/{Loop}]: unhandled error", region, loop.Name);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), ct);
        }
    }

    private async Task RunPass(string region, BackfillLoopSpec loop, CancellationToken ct)
    {
        SetLoopState(region, loop.Name, StateRunning);
        var faulted = false;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tuning = _backfillOptions.Tuning;

            var itemIds = await _catalog.GetMarketableItemIdsAsync(ct);
            var bucketCount = BackfillBuckets.BucketCountFor(itemIds.Count, tuning.ItemsPerRequest);
            var groups = BackfillBuckets.Group(itemIds, bucketCount);

            var states = await LoadOrSeedStates(region, loop.Name, bucketCount, now, ct);

            var skipped = 0;
            var work = new List<BucketWork>();

            foreach (var group in groups)
            {
                if (!states.TryGetValue(group.Key, out var state) || state.CrawlComplete)
                    continue;

                var window = loop.SelectWindow(_backfillOptions, state, now);
                if (window is null)
                {
                    skipped++;
                    continue;
                }

                work.Add(new BucketWork(state, group.Value, window.Value));
            }

            var outcomes = new ConcurrentBag<BackfillBucketOutcome>();

            // An unmoved pointer is what makes a bucket retry next pass, so a chunk counts as done
            // only once its rows are written and its pointer moved.
            var stalled = await BackfillChunkRunner.RunAsync(
                work,
                async (bucket, token) =>
                {
                    var sales = await FetchBucket(region, bucket, token);
                    if (sales is null)
                        return false;

                    outcomes.Add(await SettleBucket(loop, bucket, sales, token));
                    return true;
                },
                tuning.RetryRounds,
                tuning.Concurrency,
                TimeSpan.FromSeconds(tuning.RetryRoundDelaySeconds),
                _rateLimiter.ConsumeAsync,
                ct);

            MetricsCatalog.BackfillStalledBuckets.WithLabels(region, loop.Name).Set(stalled);
            if (stalled > 0)
                MetricsCatalog.BackfillPointerStalledTotal.WithLabels(region, loop.Name).Inc(stalled);

            _logger.LogInformation(
                "Backfill [{Region}/{Loop}]: {Advanced} advanced, {Stalled} stalled, {Skipped} skipped, {Completed} finished their history",
                region, loop.Name,
                outcomes.Count(o => o == BackfillBucketOutcome.Advanced),
                stalled,
                skipped,
                outcomes.Count(o => o == BackfillBucketOutcome.Complete));

            if (loop.TracksHistoryDepth)
                await ReportHistoryDepth(region, now, ct);
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            SetLoopState(region, loop.Name, faulted ? StateError : StateIdle);
        }
    }

    private Task<List<Sale>?> FetchBucket(string region, BucketWork bucket, CancellationToken ct)
    {
        var entriesWithinSeconds = BackfillWindow.EntriesWithinSeconds(bucket.Window.Start, bucket.Window.End);
        var requestTimeout = _backfillOptions.Tuning.RequestTimeoutFor(entriesWithinSeconds);

        return FetchChunk(region, bucket.Items, entriesWithinSeconds, requestTimeout, ct);
    }

    private async Task<BackfillBucketOutcome> SettleBucket(
        BackfillLoopSpec loop,
        BucketWork bucket,
        List<Sale> sales,
        CancellationToken ct)
    {
        var olderThan = bucket.Window.OlderThan;
        var toWrite = olderThan is null
            ? sales
            : sales.Where(s => s.SaleTime < olderThan.Value).ToList();

        if (toWrite.Count > 0)
        {
            await _saleWriter.AddBatchAsync(toWrite, ct);
            await EnqueueDirtyPairs(toWrite, ct);
        }

        var (next, outcome) = loop.Advance(bucket.State, bucket.Window, toWrite.Count > 0);
        await _stateStore.UpsertBucketAsync(next, ct);

        return outcome;
    }

    /// <summary>Buckets advance independently, so the depth guaranteed across the catalogue is the
    /// one furthest behind.</summary>
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
            // The budget spans the Polly retries, not one response, so exhausting it usually means
            // repeated transient 5xx rather than a slow server.
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

    public override void Dispose()
    {
        _rateLimiter.Dispose();
        base.Dispose();
    }
}
