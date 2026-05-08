using Cassandra;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using WsWorker.Models;
using WsWorker.Options;
using WsWorker.Services;

namespace WsWorker.Workers;

public sealed class SalesBackfillService : BackgroundService
{
    private readonly ScyllaService _scyllaService;
    private readonly WorldDataCache _worldDataCache;
    private readonly UniversalisOptions _uniOptions;
    private readonly BackfillOptions _backfillOptions;
    private readonly BackendOptions _backendOptions;
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
        ScyllaService scyllaService,
        WorldDataCache worldDataCache,
        IOptions<UniversalisOptions> uniOptions,
        IOptions<BackfillOptions> backfillOptions,
        IOptions<BackendOptions> backendOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<SalesBackfillService> logger)
    {
        _scyllaService = scyllaService;
        _worldDataCache = worldDataCache;
        _uniOptions = uniOptions.Value;
        _backfillOptions = backfillOptions.Value;
        _backendOptions = backendOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _rateLimiter = new TokenBucket(
            capacity: _uniOptions.MaxRequestsPerSecondBurst,
            refillRate: _uniOptions.MaxRequestsPerSecond);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _scyllaService.InitializeAsync();
        await _worldDataCache.InitializeAsync();

        _selectState = await _scyllaService.PrepareAsync(SelectStateCql);
        _upsertState = await _scyllaService.PrepareAsync(UpsertStateCql);

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

        var entriesWithin = (long)gap.TotalMilliseconds;
        _logger.LogInformation("LiveGapFillLoop [{Region}]: fetching {Gap:F1} min of history", region, gap.TotalMinutes);

        var sales = await FetchHistory(region, entriesWithin, ct);
        _logger.LogInformation("LiveGapFillLoop [{Region}]: fetched {Count} sales", region, sales.Count);

        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();
        var gilfluxPairs = new HashSet<(int WorldId, int ItemId)>();
        foreach (var s in sales)
        {
            if (s.SaleTime > sevenDaysAgo)
                gilfluxPairs.Add((s.WorldId, s.ItemId));
        }

        await WriteSales(sales, ct);
        await WriteState(region, lastImportAt: now, earliestImportAt: null);

        await RunGilfluxRefreshPass(gilfluxPairs, ct);
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
        var now = DateTimeOffset.UtcNow;

        var (_, earliestImportAt) = await ReadState(region);

        if (earliestImportAt is null)
        {
            await WriteState(region, lastImportAt: null, earliestImportAt: now);
            _logger.LogInformation("HistoricalCrawlLoop [{Region}]: first run — initialised earliest_import_at pointer", region);
            return;
        }

        var chunkStart = earliestImportAt.Value - TimeSpan.FromDays(_backfillOptions.ChunkDays);
        var entriesWithin = (long)(now - chunkStart).TotalMilliseconds;

        _logger.LogInformation(
            "HistoricalCrawlLoop [{Region}]: crawling chunk {ChunkStart:u} → {EarliestImportAt:u}",
            region, chunkStart, earliestImportAt.Value);

        var allSales = await FetchHistory(region, entriesWithin, ct);
        _logger.LogInformation("HistoricalCrawlLoop [{Region}]: fetched {Count} total sales for chunk", region, allSales.Count);

        var earliestMs = earliestImportAt.Value.ToUnixTimeMilliseconds();
        var toWrite = allSales.Where(s => s.SaleTime < earliestMs).ToList();

        if (toWrite.Count == 0)
        {
            _logger.LogInformation("HistoricalCrawlLoop [{Region}]: 0 new entries written — crawl complete", region);
            _crawlComplete.Add(region);
            return;
        }

        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();
        var gilfluxPairs = new HashSet<(int WorldId, int ItemId)>();
        foreach (var s in toWrite)
        {
            if (s.SaleTime > sevenDaysAgo)
                gilfluxPairs.Add((s.WorldId, s.ItemId));
        }

        await WriteSales(toWrite, ct);
        await WriteState(region, lastImportAt: null, earliestImportAt: chunkStart);

        await RunGilfluxRefreshPass(gilfluxPairs, ct);
    }

    private async Task<(DateTimeOffset? LastImportAt, DateTimeOffset? EarliestImportAt)> ReadState(string region)
    {
        var rows = await _scyllaService.ExecuteAsync(_selectState.Bind(region));
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
        // Read current values so we don't clobber columns we're not updating
        var (currentLast, currentEarliest) = await ReadState(region);

        var newLast = lastImportAt ?? currentLast;
        var newEarliest = earliestImportAt ?? currentEarliest;

        await _scyllaService.ExecuteAsync(_upsertState.Bind(region, newLast, newEarliest));
    }

    private async Task<List<Sale>> FetchHistory(string region, long entriesWithin, CancellationToken ct)
    {
        var itemIds = _worldDataCache.MarketableItemIds;
        var chunks = Chunk(itemIds, _uniOptions.ItemsPerRequest);

        var results = new System.Collections.Concurrent.ConcurrentBag<Sale>();
        using var semaphore = new SemaphoreSlim(8, 8);

        var tasks = chunks.Select(async chunk =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await _rateLimiter.ConsumeAsync(ct);
                var chunkSales = await FetchChunk(region, chunk, entriesWithin, ct);
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

    private async Task<List<Sale>> FetchChunk(string region, IReadOnlyList<int> itemIds, long entriesWithin, CancellationToken ct)
    {
        var itemIdStr = string.Join(",", itemIds);
        var url = $"{_uniOptions.ApiUrl}history/{region}/{itemIdStr}?entriesWithin={entriesWithin}&entriesToReturn=999999";

        var client = _httpClientFactory.CreateClient("backfill_universalis");
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("FetchChunk [{Region}] HTTP request failed: {Message}", region, ex.Message);
            return [];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("FetchChunk [{Region}] timed out for items {Items}", region, itemIdStr);
            return [];
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
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
                _logger.LogWarning(ex, "FetchChunk [{Region}] failed reading response body", region);
                return [];
            }

            try
            {
                return ParseHistoryResponse(json);
            }
            catch (Exception ex)
            {
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

    private void ParseItemElement(JsonElement itemEl, List<Sale> sales)
    {
        if (!itemEl.TryGetProperty("itemID", out var itemIdEl) ||
            !itemEl.TryGetProperty("entries", out var entriesEl) ||
            entriesEl.ValueKind != JsonValueKind.Array)
            return;

        var itemId = itemIdEl.GetInt32();

        foreach (var entry in entriesEl.EnumerateArray())
        {
            int worldId = 0;
            if (entry.TryGetProperty("worldID", out var wIdEl))
                worldId = wIdEl.GetInt32();

            var world = _worldDataCache.GetWorld(worldId);
            var worldName = world?.Name ?? (entry.TryGetProperty("worldName", out var wn) ? wn.GetString() ?? string.Empty : string.Empty);
            var datacenter = world?.Datacenter ?? string.Empty;
            var worldRegion = world?.Region ?? string.Empty;

            var itemName = _worldDataCache.GetItemName(itemId) ?? string.Empty;

            var hq = entry.TryGetProperty("hq", out var hqEl) && hqEl.ValueKind == JsonValueKind.True;
            var onMannequin = entry.TryGetProperty("onMannequin", out var omEl) && omEl.ValueKind == JsonValueKind.True;
            var pricePerUnit = entry.TryGetProperty("pricePerUnit", out var ppuEl) ? ppuEl.GetInt32() : 0;
            var quantity = entry.TryGetProperty("quantity", out var qEl) ? qEl.GetInt32() : 0;
            var buyerName = entry.TryGetProperty("buyerName", out var bnEl) ? bnEl.GetString() ?? string.Empty : string.Empty;
            var total = entry.TryGetProperty("total", out var totEl) ? totEl.GetInt32() : pricePerUnit * quantity;

            long saleTimeMs = 0;
            if (entry.TryGetProperty("timestamp", out var tsEl))
                saleTimeMs = tsEl.GetInt64() * 1000L; // Universalis history timestamps are in seconds

            sales.Add(new Sale
            {
                BuyerName = buyerName,
                Hq = hq,
                OnMannequin = onMannequin,
                UnitPrice = pricePerUnit,
                Quantity = quantity,
                SaleTime = saleTimeMs,
                WorldId = worldId,
                ItemId = itemId,
                WorldName = worldName,
                ItemName = itemName,
                Datacenter = datacenter,
                Region = worldRegion,
                Total = total,
            });
        }
    }

    private async Task WriteSales(IEnumerable<Sale> sales, CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(16, 16);
        var tasks = sales.Select(async sale =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var bound = _scyllaService.SalesInsert.Bind(
                    sale.BuyerName,
                    sale.Hq,
                    sale.OnMannequin,
                    sale.UnitPrice,
                    sale.Quantity,
                    sale.SaleTime,
                    sale.WorldId,
                    sale.ItemId,
                    sale.WorldName,
                    sale.ItemName,
                    sale.Datacenter,
                    sale.Region,
                    sale.Total);

                await _scyllaService.ExecuteAsync(bound);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var allTasks = tasks.ToList();
        await Task.WhenAll(allTasks);

        _logger.LogInformation("WriteSales: wrote {Count} sales to Scylla", allTasks.Count);
    }

    private async Task RunGilfluxRefreshPass(HashSet<(int WorldId, int ItemId)> pairs, CancellationToken ct)
    {
        if (pairs.Count == 0)
            return;

        _logger.LogInformation("RunGilfluxRefreshPass: refreshing {Count} (worldId, itemId) pairs", pairs.Count);

        using var semaphore = new SemaphoreSlim(8, 8);
        var client = _httpClientFactory.CreateClient("backfill_gilflux");

        var tasks = pairs.Select(async pair =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var url = $"http://{_backendOptions.Host}/api/v1/updatedb/gilflux_ranking_update/{pair.WorldId}/{pair.ItemId}";
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    using var response = await client.GetAsync(url, cts.Token);
                    if (!response.IsSuccessStatusCode)
                        _logger.LogWarning("GilfluxRefresh: {StatusCode} for world={WorldId} item={ItemId}", (int)response.StatusCode, pair.WorldId, pair.ItemId);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("GilfluxRefresh: connection error for world={WorldId} item={ItemId}: {Message}", pair.WorldId, pair.ItemId, ex.Message);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("GilfluxRefresh: timeout for world={WorldId} item={ItemId}", pair.WorldId, pair.ItemId);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
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
