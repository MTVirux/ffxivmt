using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using WsWorker.Options;

namespace WsWorker.Workers;

public sealed class SalesBackfillService : BackgroundService
{
    private readonly IScyllaSession _scylla;
    private readonly ISaleStore _saleStore;
    private readonly WorldStructureService _catalog;
    private readonly IDirtyPairQueue _dirtyPairs;
    private readonly UniversalisOptions _uniOptions;
    private readonly BackfillOptions _backfillOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SalesBackfillService> _logger;

    private PreparedStatement _selectState = null!;
    private PreparedStatement _upsertState = null!;

    private readonly TokenBucket _rateLimiter;

    private const string SelectStateCql =
        "SELECT last_import_at, earliest_import_at FROM ffmt.backfill_state WHERE region = ?";

    private const string UpsertStateCql =
        "INSERT INTO ffmt.backfill_state (region, last_import_at, earliest_import_at) VALUES (?, ?, ?)";

    private readonly HashSet<string> _crawlComplete = new(StringComparer.OrdinalIgnoreCase);

    public SalesBackfillService(
        IScyllaSession scylla,
        ISaleStore saleStore,
        WorldStructureService catalog,
        IDirtyPairQueue dirtyPairs,
        IOptions<UniversalisOptions> uniOptions,
        IOptions<BackfillOptions> backfillOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<SalesBackfillService> logger)
    {
        _scylla = scylla;
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
        _selectState = await _scylla.PrepareAsync(SelectStateCql, ct);
        _upsertState = await _scylla.PrepareAsync(UpsertStateCql, ct);

        _logger.LogInformation("SalesBackfillService initialized — starting live-gap and historical crawl loops");

        await Task.WhenAll(
            LiveGapFillLoop(ct),
            HistoricalCrawlLoop(ct));
    }

    private async Task LiveGapFillLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("LiveGapFillLoop: starting pass");

            foreach (var region in _uniOptions.RegionsToImport)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    await RunLiveGapPass(region, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LiveGapFillLoop: unhandled error for region {Region}", region);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(_backfillOptions.LiveGapIntervalMinutes), ct);
        }
    }

    private async Task RunLiveGapPass(string region, CancellationToken ct)
    {
        MetricsCatalog.BackfillState.WithLabels(region).Set(1); // running
        try
        {
            var now = DateTimeOffset.UtcNow;

            var (lastImportAt, _) = await ReadState(region);

            if (lastImportAt is null)
            {
                await WriteState(region, lastImportAt: now, earliestImportAt: null);
                _logger.LogInformation("LiveGapFillLoop [{Region}]: first run — initialised last_import_at pointer, skipping fetch", region);
                return;
            }

            var gap = now - lastImportAt.Value;
            if (gap < TimeSpan.FromMinutes(_backfillOptions.SkipIfGapUnderMinutes))
            {
                _logger.LogInformation("LiveGapFillLoop [{Region}]: gap {Gap:F1} min < threshold, skipping", region, gap.TotalMinutes);
                return;
            }

            var entriesWithinSeconds = (long)gap.TotalSeconds;
            _logger.LogInformation("LiveGapFillLoop [{Region}]: fetching {Gap:F1} min of history", region, gap.TotalMinutes);

            var sales = await FetchHistory(region, entriesWithinSeconds, ct);
            _logger.LogInformation("LiveGapFillLoop [{Region}]: fetched {Count} sales", region, sales.Count);

            var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
            var dirtyPairs = sales
                .Where(s => s.SaleTime > sevenDaysAgo)
                .Select(s => (s.WorldId, s.ItemId))
                .ToHashSet();

            await _saleStore.AddBatchAsync(sales, ct);
            await WriteState(region, lastImportAt: now, earliestImportAt: null);

            if (dirtyPairs.Count > 0)
            {
                await _dirtyPairs.EnqueueManyAsync(dirtyPairs, ct);
                _logger.LogInformation("LiveGapFillLoop [{Region}]: enqueued {Count} dirty pairs", region, dirtyPairs.Count);
            }
        }
        catch
        {
            MetricsCatalog.BackfillState.WithLabels(region).Set(3); // error
            throw;
        }
        finally
        {
            if (MetricsCatalog.BackfillState.WithLabels(region).Value != 3)
            {
                MetricsCatalog.BackfillState.WithLabels(region).Set(0); // idle
            }
        }
    }

    private async Task HistoricalCrawlLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("HistoricalCrawlLoop: starting pass");

            foreach (var region in _uniOptions.RegionsToImport)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (_crawlComplete.Contains(region))
                    continue;

                try
                {
                    await RunHistoricalCrawlPass(region, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HistoricalCrawlLoop: unhandled error for region {Region}", region);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(_backfillOptions.HistoricalCrawlIntervalMinutes), ct);
        }
    }

    private async Task RunHistoricalCrawlPass(string region, CancellationToken ct)
    {
        MetricsCatalog.BackfillState.WithLabels(region).Set(1); // running
        try
        {
            var now = DateTimeOffset.UtcNow;

            var (_, earliestImportAt) = await ReadState(region);

            if (earliestImportAt is null)
            {
                await WriteState(region, lastImportAt: null, earliestImportAt: now);
                _logger.LogInformation("HistoricalCrawlLoop [{Region}]: first run — initialised earliest_import_at pointer", region);
                return;
            }

            var chunkStart = earliestImportAt.Value - TimeSpan.FromDays(_backfillOptions.ChunkDays);
            var entriesWithinSeconds = (long)(now - chunkStart).TotalSeconds;

            _logger.LogInformation(
                "HistoricalCrawlLoop [{Region}]: crawling chunk {ChunkStart:u} → {EarliestImportAt:u}",
                region, chunkStart, earliestImportAt.Value);

            var allSales = await FetchHistory(region, entriesWithinSeconds, ct);
            _logger.LogInformation("HistoricalCrawlLoop [{Region}]: fetched {Count} total sales for chunk", region, allSales.Count);

            var toWrite = allSales.Where(s => s.SaleTime < earliestImportAt.Value).ToList();

            if (toWrite.Count == 0)
            {
                _logger.LogInformation("HistoricalCrawlLoop [{Region}]: 0 new entries written — crawl complete", region);
                _crawlComplete.Add(region);
                return;
            }

            var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
            var dirtyPairs = toWrite
                .Where(s => s.SaleTime > sevenDaysAgo)
                .Select(s => (s.WorldId, s.ItemId))
                .ToHashSet();

            await _saleStore.AddBatchAsync(toWrite, ct);
            await WriteState(region, lastImportAt: null, earliestImportAt: chunkStart);

            if (dirtyPairs.Count > 0)
            {
                await _dirtyPairs.EnqueueManyAsync(dirtyPairs, ct);
                _logger.LogInformation("HistoricalCrawlLoop [{Region}]: enqueued {Count} dirty pairs", region, dirtyPairs.Count);
            }
        }
        catch
        {
            MetricsCatalog.BackfillState.WithLabels(region).Set(3); // error
            throw;
        }
        finally
        {
            if (MetricsCatalog.BackfillState.WithLabels(region).Value != 3)
            {
                MetricsCatalog.BackfillState.WithLabels(region).Set(0); // idle
            }
        }
    }

    private async Task<(DateTimeOffset? LastImportAt, DateTimeOffset? EarliestImportAt)> ReadState(string region)
    {
        var rows = await _scylla.Session.ExecuteAsync(_selectState.Bind(region));
        var row = rows.FirstOrDefault();
        if (row is null)
            return (null, null);

        DateTimeOffset? lastImportAt = null;
        DateTimeOffset? earliestImportAt = null;

        try
        {
            var raw = row.GetValue<DateTimeOffset?>("last_import_at");
            if (raw.HasValue)
                lastImportAt = raw;
        }
        catch { /* column null */ }

        try
        {
            var raw = row.GetValue<DateTimeOffset?>("earliest_import_at");
            if (raw.HasValue)
                earliestImportAt = raw;
        }
        catch { /* column null */ }

        return (lastImportAt, earliestImportAt);
    }

    private async Task WriteState(string region, DateTimeOffset? lastImportAt, DateTimeOffset? earliestImportAt)
    {
        var (currentLast, currentEarliest) = await ReadState(region);
        var newLast = lastImportAt ?? currentLast;
        var newEarliest = earliestImportAt ?? currentEarliest;
        await _scylla.Session.ExecuteAsync(_upsertState.Bind(region, newLast, newEarliest));
    }

    private async Task<List<Sale>> FetchHistory(string region, long entriesWithinSeconds, CancellationToken ct)
    {
        var itemIds = await _catalog.GetMarketableItemIdsAsync(ct);
        var chunks = Chunk(itemIds, _uniOptions.ItemsPerRequest);

        var results = new System.Collections.Concurrent.ConcurrentBag<Sale>();
        using var semaphore = new SemaphoreSlim(8, 8);

        var tasks = chunks.Select(async chunk =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await _rateLimiter.ConsumeAsync(ct);
                var chunkSales = await FetchChunk(region, chunk, entriesWithinSeconds, ct);
                foreach (var s in chunkSales)
                    results.Add(s);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<List<Sale>> FetchChunk(string region, IReadOnlyList<int> itemIds, long entriesWithinSeconds, CancellationToken ct)
    {
        var itemIdStr = string.Join(",", itemIds);
        var url = $"{_uniOptions.BaseUrl.TrimEnd('/')}/history/{region}/{itemIdStr}?entriesWithin={entriesWithinSeconds}&entriesToReturn=99999";

        var client = _httpClientFactory.CreateClient("backfill_universalis");
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
            _logger.LogWarning("FetchChunk [{Region}] HTTP request failed: {Message}", region, ex.Message);
            return [];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
            _logger.LogWarning("FetchChunk [{Region}] timed out for items {Items}", region, itemIdStr);
            return [];
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning("FetchChunk [{Region}] returned {StatusCode} for {Url}", region, (int)response.StatusCode, url);
                return [];
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning(ex, "FetchChunk [{Region}] failed reading response body", region);
                return [];
            }

            try
            {
                var sales = ParseHistoryResponse(json);
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "ok").Inc();
                MetricsCatalog.BackfillRowsTotal.WithLabels(region).Inc(sales.Count);
                return sales;
            }
            catch (Exception ex)
            {
                MetricsCatalog.BackfillPagesTotal.WithLabels(region, "error").Inc();
                _logger.LogWarning(ex, "FetchChunk [{Region}] failed parsing JSON", region);
                return [];
            }
        }
    }

    private List<Sale> ParseHistoryResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sales = new List<Sale>();

        if (root.TryGetProperty("items", out var itemsArray) && itemsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in itemsArray.EnumerateArray())
                ParseItemElement(itemEl, sales);
        }
        else if (root.TryGetProperty("entries", out _))
        {
            ParseItemElement(root, sales);
        }

        return sales;
    }

    private static void ParseItemElement(JsonElement itemEl, List<Sale> sales)
    {
        if (!itemEl.TryGetProperty("itemID", out var itemIdEl) ||
            !itemEl.TryGetProperty("entries", out var entriesEl) ||
            entriesEl.ValueKind != JsonValueKind.Array)
            return;

        var itemId = itemIdEl.GetInt32();

        foreach (var entry in entriesEl.EnumerateArray())
        {
            var worldId = entry.TryGetProperty("worldID", out var wIdEl) ? wIdEl.GetInt32() : 0;
            var hq = entry.TryGetProperty("hq", out var hqEl) && hqEl.ValueKind == JsonValueKind.True;
            var onMannequin = entry.TryGetProperty("onMannequin", out var omEl) && omEl.ValueKind == JsonValueKind.True;
            var pricePerUnit = entry.TryGetProperty("pricePerUnit", out var ppuEl) ? ppuEl.GetInt32() : 0;
            var quantity = entry.TryGetProperty("quantity", out var qEl) ? qEl.GetInt32() : 0;
            var buyerName = entry.TryGetProperty("buyerName", out var bnEl) ? bnEl.GetString() ?? string.Empty : string.Empty;
            var saleTimeSeconds = entry.TryGetProperty("timestamp", out var tsEl) ? tsEl.GetInt64() : 0L;

            sales.Add(new Sale(
                ItemId:      itemId,
                WorldId:     worldId,
                BuyerName:   buyerName,
                Hq:          hq,
                OnMannequin: onMannequin,
                Quantity:    quantity,
                UnitPrice:   pricePerUnit,
                SaleTime:    DateTimeOffset.FromUnixTimeSeconds(saleTimeSeconds)));
        }
    }

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
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
