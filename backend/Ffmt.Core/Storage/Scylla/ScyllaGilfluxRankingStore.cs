using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaGilfluxRankingStore(IScyllaSession scylla) : IGilfluxRankingStore
{
    private const string CqlByWorld = """
        SELECT
            item_id, item_name, world_id, world_name, datacenter, region,
            CAST(SUM(ranking_alltime) AS BIGINT) AS ranking_alltime,
            CAST(SUM(ranking_1h)      AS BIGINT) AS ranking_1h,
            CAST(SUM(ranking_3h)      AS BIGINT) AS ranking_3h,
            CAST(SUM(ranking_6h)      AS BIGINT) AS ranking_6h,
            CAST(SUM(ranking_12h)     AS BIGINT) AS ranking_12h,
            CAST(SUM(ranking_1d)      AS BIGINT) AS ranking_1d,
            CAST(SUM(ranking_3d)      AS BIGINT) AS ranking_3d,
            CAST(SUM(ranking_7d)      AS BIGINT) AS ranking_7d,
            MAX(updated_at)     AS updated_at,
            MAX(last_sale_time) AS last_sale_time
        FROM gilflux_ranking
        WHERE world_id = ?
        GROUP BY item_id
        """;

    private const string CqlByDatacenter = """
        SELECT
            item_id, item_name, datacenter, region,
            CAST(SUM(ranking_alltime) AS BIGINT) AS ranking_alltime,
            CAST(SUM(ranking_1h)      AS BIGINT) AS ranking_1h,
            CAST(SUM(ranking_3h)      AS BIGINT) AS ranking_3h,
            CAST(SUM(ranking_6h)      AS BIGINT) AS ranking_6h,
            CAST(SUM(ranking_12h)     AS BIGINT) AS ranking_12h,
            CAST(SUM(ranking_1d)      AS BIGINT) AS ranking_1d,
            CAST(SUM(ranking_3d)      AS BIGINT) AS ranking_3d,
            CAST(SUM(ranking_7d)      AS BIGINT) AS ranking_7d,
            MAX(updated_at)     AS updated_at,
            MAX(last_sale_time) AS last_sale_time
        FROM gilflux_ranking
        WHERE datacenter = ?
        GROUP BY item_id
        """;

    private const string CqlByRegion = """
        SELECT
            item_id, item_name, region,
            CAST(SUM(ranking_alltime) AS BIGINT) AS ranking_alltime,
            CAST(SUM(ranking_1h)      AS BIGINT) AS ranking_1h,
            CAST(SUM(ranking_3h)      AS BIGINT) AS ranking_3h,
            CAST(SUM(ranking_6h)      AS BIGINT) AS ranking_6h,
            CAST(SUM(ranking_12h)     AS BIGINT) AS ranking_12h,
            CAST(SUM(ranking_1d)      AS BIGINT) AS ranking_1d,
            CAST(SUM(ranking_3d)      AS BIGINT) AS ranking_3d,
            CAST(SUM(ranking_7d)      AS BIGINT) AS ranking_7d,
            MAX(updated_at)     AS updated_at,
            MAX(last_sale_time) AS last_sale_time
        FROM gilflux_ranking
        WHERE region = ?
        GROUP BY item_id
        """;

    private const string CqlByItem = """
        SELECT
            item_id, item_name, world_id, world_name, datacenter, region,
            ranking_alltime, ranking_1h, ranking_3h, ranking_6h, ranking_12h, ranking_1d, ranking_3d, ranking_7d,
            updated_at, last_sale_time
        FROM gilflux_ranking
        WHERE item_id = ?
        ALLOW FILTERING
        """;

    private const string CqlByItemAndWorld = """
        SELECT
            item_id, item_name, world_id, world_name, datacenter, region,
            ranking_alltime, ranking_1h, ranking_3h, ranking_6h, ranking_12h, ranking_1d, ranking_3d, ranking_7d,
            updated_at, last_sale_time
        FROM gilflux_ranking
        WHERE item_id = ? AND world_id = ?
        """;

    // Aggregating SELECT over sales for one timeframe. Identical shape per timeframe (only the
    // sale_time floor differs), so we prepare it once and reuse it for all 7 timeframes.
    // The 7d variant additionally needs MAX(sale_time); we run that as a second tiny query
    // rather than carry an unused column on the other six.
    private const string CqlSumTotalSinceTimeframe = """
        SELECT CAST(SUM(total) AS BIGINT) AS gilflux
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

    private const string CqlUpsertRanking = """
        INSERT INTO gilflux_ranking
            (item_id, world_id, datacenter, region, item_name, world_name,
             ranking_alltime, ranking_1h, ranking_3h, ranking_6h, ranking_12h,
             ranking_1d, ranking_3d, ranking_7d, last_sale_time, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const string CqlGetItemNameById = "SELECT name FROM items WHERE id = ?";
    private const string CqlGetWorldById = "SELECT name, datacenter, region FROM worlds WHERE id = ?";
    private const string CqlGetOneMissingName = "SELECT item_id FROM gilflux_ranking WHERE item_name = '' LIMIT 1 ALLOW FILTERING";

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
    private readonly RequestCoalescer<string, IReadOnlyList<GilfluxRanking>> _dcCoalescer = new();
    private readonly RequestCoalescer<string, IReadOnlyList<GilfluxRanking>> _regionCoalescer = new();
    private readonly RequestCoalescer<int, IReadOnlyList<GilfluxRanking>> _itemCoalescer = new();
    private readonly RequestCoalescer<(int, int), IReadOnlyList<GilfluxRanking>> _itemWorldCoalescer = new();

    public Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default) =>
        _worldCoalescer.CoalesceAsync(worldId, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByWorld).ConfigureAwait(false);
            return await ExecuteAsync(stmt.Bind(worldId)).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByDatacenterAsync(string datacenter, CancellationToken ct = default) =>
        _dcCoalescer.CoalesceAsync(datacenter, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByDatacenter).ConfigureAwait(false);
            return await ExecuteAsync(stmt.Bind(datacenter)).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByRegionAsync(string region, CancellationToken ct = default) =>
        _regionCoalescer.CoalesceAsync(region, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByRegion).ConfigureAwait(false);
            return await ExecuteAsync(stmt.Bind(region)).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default) =>
        _itemCoalescer.CoalesceAsync(itemId, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByItem).ConfigureAwait(false);
            return await ExecuteAsync(stmt.Bind(itemId)).ConfigureAwait(false);
        });

    public Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default) =>
        _itemWorldCoalescer.CoalesceAsync((itemId, worldId), async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByItemAndWorld).ConfigureAwait(false);
            return await ExecuteAsync(stmt.Bind(itemId, worldId)).ConfigureAwait(false);
        });

    public async Task UpdateRankingAsync(int worldId, int itemId, CancellationToken ct = default)
    {
        var sumStmt = await scylla.PrepareAsync(CqlSumTotalSinceTimeframe, ct).ConfigureAwait(false);
        var maxStmt = await scylla.PrepareAsync(CqlMaxSaleTime, ct).ConfigureAwait(false);
        var nameStmt = await scylla.PrepareAsync(CqlGetItemNameById, ct).ConfigureAwait(false);
        var worldStmt = await scylla.PrepareAsync(CqlGetWorldById, ct).ConfigureAwait(false);
        var insertStmt = await scylla.PrepareAsync(CqlUpsertRanking, ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;

        var sumTasks = Timeframes
            .Select(tf => scylla.Session.ExecuteAsync(sumStmt.Bind(itemId, worldId, now - tf)))
            .ToArray();
        var maxSaleTask = scylla.Session.ExecuteAsync(maxStmt.Bind(itemId, worldId, now - Timeframes[^1]));
        var itemNameTask = scylla.Session.ExecuteAsync(nameStmt.Bind(itemId));
        var worldTask = scylla.Session.ExecuteAsync(worldStmt.Bind(worldId));

        await Task.WhenAll(sumTasks.Concat(new[] { maxSaleTask, itemNameTask, worldTask })).ConfigureAwait(false);

        var sums = sumTasks.Select(t => SumGilflux(t.Result)).ToArray();
        var lastSaleTime = MaxLastSaleTime(maxSaleTask.Result);

        var itemRow = itemNameTask.Result.FirstOrDefault();
        var itemName = itemRow is null ? string.Empty : itemRow.GetValue<string>("name") ?? string.Empty;

        var worldRow = worldTask.Result.FirstOrDefault();
        var worldName = worldRow?.GetValue<string>("name") ?? string.Empty;
        var datacenter = worldRow?.GetValue<string>("datacenter") ?? string.Empty;
        var region = worldRow?.GetValue<string>("region") ?? string.Empty;

        await scylla.Session.ExecuteAsync(insertStmt.Bind(
            itemId, worldId, datacenter, region, itemName, worldName,
            0L,
            sums[0], sums[1], sums[2], sums[3], sums[4], sums[5], sums[6],
            lastSaleTime ?? DateTimeOffset.FromUnixTimeMilliseconds(0),
            now)).ConfigureAwait(false);
    }

    public async Task<int?> GetOneItemIdWithMissingNameAsync(CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetOneMissingName, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var row = rows.FirstOrDefault();
        if (row is null || row.IsNull("item_id"))
        {
            return null;
        }
        return row.GetValue<int>("item_id");
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

    private async Task<IReadOnlyList<GilfluxRanking>> ExecuteAsync(IStatement stmt)
    {
        var rows = await scylla.Session.ExecuteAsync(stmt).ConfigureAwait(false);
        var result = new List<GilfluxRanking>();
        foreach (var row in rows)
        {
            result.Add(MapRow(row));
        }
        return result;
    }

    private static GilfluxRanking MapRow(Row row) => new(
        ItemId:       row.GetValue<int>("item_id"),
        WorldId:      HasColumn(row, "world_id") && !row.IsNull("world_id") ? row.GetValue<int>("world_id") : null,
        Ranking1h:    GetLong(row, "ranking_1h"),
        Ranking3h:    GetLong(row, "ranking_3h"),
        Ranking6h:    GetLong(row, "ranking_6h"),
        Ranking12h:   GetLong(row, "ranking_12h"),
        Ranking1d:    GetLong(row, "ranking_1d"),
        Ranking3d:    GetLong(row, "ranking_3d"),
        Ranking7d:    GetLong(row, "ranking_7d"),
        UpdatedAt:    GetNullableLong(row, "updated_at"),
        LastSaleTime: GetNullableLong(row, "last_sale_time"));

    private static bool HasColumn(Row row, string name) => row.GetColumn(name) is not null;

    private static string? GetStr(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<string>(name) : null;

    private static long GetLong(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<long>(name) : 0L;

    private static long? GetNullableLong(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<long>(name) : null;
}
