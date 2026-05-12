using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Metrics;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.Gilflux;

public sealed class ScyllaRankingRefresher(IScyllaSession scylla, ILogger<ScyllaRankingRefresher> logger) : IRankingRefresher
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

    private const string CqlUpsertGilfluxRanking = """
        INSERT INTO gilflux_ranking
            (item_id, world_id,
             ranking_1h, ranking_3h, ranking_6h, ranking_12h,
             ranking_1d, ranking_3d, ranking_7d,
             last_sale_time, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const string CqlUpsertGilfluxByWorld = """
        INSERT INTO gilflux_by_world
            (world_id, item_id,
             ranking_1h, ranking_3h, ranking_6h, ranking_12h,
             ranking_1d, ranking_3d, ranking_7d,
             last_sale_time, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private static readonly TimeSpan[] Timeframes =
    [
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
    ];

    public async Task RefreshAsync(int worldId, int itemId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var sumStmt = await scylla.PrepareAsync(CqlSumTotalSinceTimeframe, ct).ConfigureAwait(false);
            var maxStmt = await scylla.PrepareAsync(CqlMaxSaleTime, ct).ConfigureAwait(false);
            var upsertRankingStmt = await scylla.PrepareAsync(CqlUpsertGilfluxRanking, ct).ConfigureAwait(false);
            var upsertByWorldStmt = await scylla.PrepareAsync(CqlUpsertGilfluxByWorld, ct).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;

            var sumTasks = Timeframes
                .Select(tf => scylla.MeasuredExecuteAsync(sumStmt.Bind(itemId, worldId, now - tf), "gilflux_sum"))
                .ToArray();
            var maxSaleTask = scylla.MeasuredExecuteAsync(maxStmt.Bind(itemId, worldId, now - Timeframes[^1]), "gilflux_max");

            await Task.WhenAll(sumTasks.Concat(new[] { maxSaleTask })).ConfigureAwait(false);

            var sums = sumTasks.Select(t => SumGilflux(t.Result)).ToArray();
            var lastSaleTime = MaxLastSaleTime(maxSaleTask.Result) ?? DateTimeOffset.FromUnixTimeMilliseconds(0);

            var batch = (BatchStatement)new BatchStatement()
                .SetBatchType(BatchType.Unlogged)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne);

            batch.Add(upsertRankingStmt.Bind(
                itemId, worldId,
                sums[0], sums[1], sums[2], sums[3], sums[4], sums[5], sums[6],
                lastSaleTime, now));

            batch.Add(upsertByWorldStmt.Bind(
                worldId, itemId,
                sums[0], sums[1], sums[2], sums[3], sums[4], sums[5], sums[6],
                lastSaleTime, now));

            await scylla.MeasuredExecuteAsync(batch, "gilflux_upsert").ConfigureAwait(false);
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
        {
            return 0L;
        }
        return row.GetValue<long>("gilflux");
    }

    private static DateTimeOffset? MaxLastSaleTime(RowSet rs)
    {
        var row = rs.FirstOrDefault();
        if (row is null || row.GetColumn("last_sale_time") is null || row.IsNull("last_sale_time"))
        {
            return null;
        }
        return row.GetValue<DateTimeOffset>("last_sale_time");
    }
}
