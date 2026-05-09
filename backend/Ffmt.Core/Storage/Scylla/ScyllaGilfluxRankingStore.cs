using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaGilfluxRankingStore(IScyllaSession scylla) : IGilfluxRankingStore
{
    private const string CqlByWorld = """
        SELECT item_id, ranking_1h, ranking_3h, ranking_6h, ranking_12h,
               ranking_1d, ranking_3d, ranking_7d, last_sale_time, updated_at
        FROM gilflux_by_world
        WHERE world_id = ?
        """;

    private const string CqlByItem = """
        SELECT item_id, world_id, ranking_1h, ranking_3h, ranking_6h, ranking_12h,
               ranking_1d, ranking_3d, ranking_7d, last_sale_time, updated_at
        FROM gilflux_ranking
        WHERE item_id = ?
        ALLOW FILTERING
        """;

    private const string CqlByItemAndWorld = """
        SELECT item_id, world_id, ranking_1h, ranking_3h, ranking_6h, ranking_12h,
               ranking_1d, ranking_3d, ranking_7d, last_sale_time, updated_at
        FROM gilflux_ranking
        WHERE item_id = ? AND world_id = ?
        """;

    // Aggregating SELECT over sales for one timeframe. Identical shape per timeframe
    // (only the sale_time floor differs), so we prepare it once and reuse across all 7.
    private const string CqlSumTotalSinceTimeframe = """
        SELECT CAST(SUM(quantity * unit_price) AS BIGINT) AS gilflux
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

    private readonly RequestCoalescer<int, IReadOnlyList<GilfluxRanking>> _worldCoalescer = new();
    private readonly RequestCoalescer<int, IReadOnlyList<GilfluxRanking>> _itemCoalescer = new();
    private readonly RequestCoalescer<(int, int), IReadOnlyList<GilfluxRanking>> _itemWorldCoalescer = new();

    public Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default) =>
        _worldCoalescer.CoalesceAsync(worldId, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByWorld).ConfigureAwait(false);
            return await ExecuteAndMapByWorldAsync(stmt.Bind(worldId), worldId).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default) =>
        _itemCoalescer.CoalesceAsync(itemId, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByItem).ConfigureAwait(false);
            return await ExecuteAndMapAsync(stmt.Bind(itemId)).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default) =>
        _itemWorldCoalescer.CoalesceAsync((itemId, worldId), async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByItemAndWorld).ConfigureAwait(false);
            return await ExecuteAndMapAsync(stmt.Bind(itemId, worldId)).ConfigureAwait(false);
        });

    public async Task UpdateRankingAsync(int worldId, int itemId, CancellationToken ct = default)
    {
        var sumStmt = await scylla.PrepareAsync(CqlSumTotalSinceTimeframe, ct).ConfigureAwait(false);
        var maxStmt = await scylla.PrepareAsync(CqlMaxSaleTime, ct).ConfigureAwait(false);
        var upsertRankingStmt = await scylla.PrepareAsync(CqlUpsertGilfluxRanking, ct).ConfigureAwait(false);
        var upsertByWorldStmt = await scylla.PrepareAsync(CqlUpsertGilfluxByWorld, ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;

        var sumTasks = Timeframes
            .Select(tf => scylla.Session.ExecuteAsync(sumStmt.Bind(itemId, worldId, now - tf)))
            .ToArray();
        var maxSaleTask = scylla.Session.ExecuteAsync(maxStmt.Bind(itemId, worldId, now - Timeframes[^1]));

        await Task.WhenAll(sumTasks.Concat(new[] { maxSaleTask })).ConfigureAwait(false);

        var sums = sumTasks.Select(t => SumGilflux(t.Result)).ToArray();
        var lastSaleTime = MaxLastSaleTime(maxSaleTask.Result) ?? DateTimeOffset.FromUnixTimeMilliseconds(0);

        // Single unlogged batch keeps both writes coordinated. They target different
        // partitions (PK ((item_id, world_id)) vs PK ((world_id))) so the coordinator
        // fan-out cost is real but small (2 partitions, both LocalOne).
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

        await scylla.Session.ExecuteAsync(batch).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<GilfluxRanking>> ExecuteAndMapAsync(IStatement stmt)
    {
        var rows = await scylla.Session.ExecuteAsync(stmt).ConfigureAwait(false);
        var result = new List<GilfluxRanking>();
        foreach (var row in rows)
        {
            result.Add(MapRow(row, worldIdOverride: null));
        }
        return result;
    }

    private async Task<IReadOnlyList<GilfluxRanking>> ExecuteAndMapByWorldAsync(IStatement stmt, int worldId)
    {
        // gilflux_by_world rows don't carry world_id (it's the partition key); inject it
        // so callers downstream see a fully-populated GilfluxRanking record.
        var rows = await scylla.Session.ExecuteAsync(stmt).ConfigureAwait(false);
        var result = new List<GilfluxRanking>();
        foreach (var row in rows)
        {
            result.Add(MapRow(row, worldIdOverride: worldId));
        }
        return result;
    }

    private static GilfluxRanking MapRow(Row row, int? worldIdOverride) => new(
        ItemId:        row.GetValue<int>("item_id"),
        WorldId:       worldIdOverride ?? (HasColumn(row, "world_id") && !row.IsNull("world_id") ? row.GetValue<int>("world_id") : null),
        Ranking1h:      GetLong(row, "ranking_1h"),
        Ranking3h:      GetLong(row, "ranking_3h"),
        Ranking6h:      GetLong(row, "ranking_6h"),
        Ranking12h:     GetLong(row, "ranking_12h"),
        Ranking1d:      GetLong(row, "ranking_1d"),
        Ranking3d:      GetLong(row, "ranking_3d"),
        Ranking7d:      GetLong(row, "ranking_7d"),
        UpdatedAt:      GetNullableEpochMs(row, "updated_at"),
        LastSaleTime:   GetNullableEpochMs(row, "last_sale_time"));

    private static bool HasColumn(Row row, string name) => row.GetColumn(name) is not null;

    private static long GetLong(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<long>(name) : 0L;

    private static long? GetNullableEpochMs(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<DateTimeOffset>(name).ToUnixTimeMilliseconds() : null;
}
