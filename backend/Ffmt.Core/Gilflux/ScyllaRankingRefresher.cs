using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed class ScyllaRankingRefresher(
    IScyllaSession scylla,
    IOptions<GilfluxOptions> options,
    ILogger<ScyllaRankingRefresher> logger) : IRankingRefresher
{
    private const string CqlSumTotalSinceTimeframe = """
        SELECT CAST(SUM(total_price) AS BIGINT) AS gilflux
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
        """;

    public async Task RefreshAsync(int worldId, int itemId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (options.Value.TimeframesMs.Count == 0)
                return;

            var timeframes = options.Value.TimeframesMs
                .Select(kv => (Key: kv.Key, Duration: TimeSpan.FromMilliseconds(kv.Value)))
                .ToArray();

            var sumStmt    = await scylla.PrepareAsync(CqlSumTotalSinceTimeframe, ct).ConfigureAwait(false);
            var maxStmt    = await scylla.PrepareAsync(CqlMaxSaleTime, ct).ConfigureAwait(false);
            var upsertStmt = await scylla.PrepareAsync(CqlUpsertGilfluxRankings, ct).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var maxDuration = TimeSpan.FromMilliseconds(options.Value.TimeframesMs.Values.Max());

            var sumTasks = timeframes
                .Select(tf => scylla.MeasuredExecuteAsync(sumStmt.Bind(itemId, worldId, now - tf.Duration), "gilflux_sum"))
                .ToArray();
            var maxSaleTask = scylla.MeasuredExecuteAsync(maxStmt.Bind(itemId, worldId, now - maxDuration), "gilflux_max");

            await Task.WhenAll(sumTasks.Concat(new[] { maxSaleTask })).ConfigureAwait(false);

            var rankings = new Dictionary<string, long>();
            for (var i = 0; i < timeframes.Length; i++)
            {
                rankings[timeframes[i].Key] = SumGilflux(sumTasks[i].Result);
            }

            var lastSaleTime = MaxLastSaleTime(maxSaleTask.Result) ?? DateTimeOffset.FromUnixTimeMilliseconds(0);

            await scylla.MeasuredExecuteAsync(
                upsertStmt.Bind(worldId, itemId, rankings, lastSaleTime, now),
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
                logger.LogWarning(ex, "RankingRefresher: refresh failed for world={WorldId} item={ItemId}", pair.WorldId, pair.ItemId);
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
