using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed class ScyllaRankingRefresher : IRankingRefresher
{
    private const string CqlSumTotalSinceTimeframe = """
        SELECT CAST(SUM(total_price_gil) AS BIGINT) AS gilflux
        FROM sales
        WHERE item_id = ? AND world_id = ? AND sale_time >= ?
        GROUP BY item_id, world_id
        """;

    private const string CqlMaxSaleTime = """
        SELECT MAX(sale_time) AS last_sale_time
        FROM sales
        WHERE item_id = ? AND world_id = ? AND sale_time >= ?
        GROUP BY item_id, world_id
        """;

    private const string CqlUpsertGilfluxRankings = """
        INSERT INTO gilflux_rankings
            (world_id, item_id, rankings, last_sale_time, updated_at)
        VALUES (?, ?, ?, ?, ?)
        USING TTL ?
        """;

    private readonly IScyllaSession _scylla;
    private readonly ILogger<ScyllaRankingRefresher> _logger;

    // Derived once - RefreshAsync runs per (world, item) sale.
    private readonly (string Key, TimeSpan Duration)[] _timeframes;
    private readonly TimeSpan _maxDuration;

    // Nothing refreshes a pair that stops selling, so the row has to expire on its own.
    private readonly int _ttlSeconds;

    public ScyllaRankingRefresher(
        IScyllaSession scylla,
        IOptions<GilfluxOptions> options,
        ILogger<ScyllaRankingRefresher> logger)
    {
        _scylla = scylla;
        _logger = logger;

        var timeframesMs = options.Value.TimeframesMs;
        _timeframes = timeframesMs
            .Select(kv => (Key: kv.Key, Duration: TimeSpan.FromMilliseconds(kv.Value)))
            .ToArray();
        _maxDuration = _timeframes.Length == 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(timeframesMs.Values.Max());
        _ttlSeconds = (int)Math.Ceiling(_maxDuration.TotalSeconds)
            + Math.Max(0, options.Value.RankingTtlGraceSeconds);
    }

    public async Task RefreshAsync(int worldId, int itemId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_timeframes.Length == 0)
                return;

            var sumStmt    = await _scylla.PrepareAsync(CqlSumTotalSinceTimeframe, ct).ConfigureAwait(false);
            var maxStmt    = await _scylla.PrepareAsync(CqlMaxSaleTime, ct).ConfigureAwait(false);
            var upsertStmt = await _scylla.PrepareAsync(CqlUpsertGilfluxRankings, ct).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;

            var sumTasks = _timeframes
                .Select(tf => _scylla.MeasuredExecuteAsync(sumStmt.Bind(itemId, worldId, now - tf.Duration), "gilflux_sum"))
                .ToArray();
            var maxSaleTask = _scylla.MeasuredExecuteAsync(maxStmt.Bind(itemId, worldId, now - _maxDuration), "gilflux_max");

            await Task.WhenAll(sumTasks.Concat(new[] { maxSaleTask })).ConfigureAwait(false);

            var rankings = new Dictionary<string, long>();
            for (var i = 0; i < _timeframes.Length; i++)
            {
                rankings[_timeframes[i].Key] = SumGilflux(sumTasks[i].Result);
            }

            var lastSaleTime = MaxLastSaleTime(maxSaleTask.Result);

            await _scylla.MeasuredExecuteAsync(
                upsertStmt.Bind(worldId, itemId, rankings, lastSaleTime, now, _ttlSeconds),
                "gilflux_upsert").ConfigureAwait(false);
        }
        catch (Exception)
        {
            MetricsCatalog.GilfluxRefreshErrorsTotal.Inc();
            throw;
        }
        finally
        {
            sw.Stop();
            MetricsCatalog.GilfluxRefreshDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        }
    }

    public async Task RefreshManyAsync(IReadOnlyCollection<(int WorldId, int ItemId)> pairs, int maxConcurrency, CancellationToken ct = default)
    {
        if (pairs.Count == 0)
        {
            return;
        }

        var concurrency = maxConcurrency <= 0 ? pairs.Count : maxConcurrency;
        using var sem = new SemaphoreSlim(concurrency, concurrency);

        var tasks = pairs.Select(async pair =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await RefreshAsync(pair.WorldId, pair.ItemId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RankingRefresher: refresh failed for world={WorldId} item={ItemId}", pair.WorldId, pair.ItemId);
            }
            finally
            {
                sem.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static long SumGilflux(RowSet rs)
    {
        var row = rs.FirstOrDefault();
        if (row is null || row.GetColumn("gilflux") is null || row.IsNull("gilflux"))
            return 0L;
        return row.GetValue<long>("gilflux");
    }

    private static DateTimeOffset? MaxLastSaleTime(RowSet rs)
    {
        var row = rs.FirstOrDefault();
        if (row is null || row.GetColumn("last_sale_time") is null || row.IsNull("last_sale_time"))
            return null;
        return row.GetValue<DateTimeOffset>("last_sale_time");
    }
}
