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

    public async Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlByWorld, ct).ConfigureAwait(false);
        return await ExecuteAsync(stmt.Bind(worldId)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GilfluxRanking>> GetByDatacenterAsync(string datacenter, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlByDatacenter, ct).ConfigureAwait(false);
        return await ExecuteAsync(stmt.Bind(datacenter)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GilfluxRanking>> GetByRegionAsync(string region, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlByRegion, ct).ConfigureAwait(false);
        return await ExecuteAsync(stmt.Bind(region)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlByItem, ct).ConfigureAwait(false);
        return await ExecuteAsync(stmt.Bind(itemId)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlByItemAndWorld, ct).ConfigureAwait(false);
        return await ExecuteAsync(stmt.Bind(itemId, worldId)).ConfigureAwait(false);
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
        ItemId:        row.GetValue<int>("item_id"),
        ItemName:      GetStr(row, "item_name") ?? string.Empty,
        WorldId:       HasColumn(row, "world_id") && !row.IsNull("world_id") ? row.GetValue<int>("world_id") : null,
        WorldName:     GetStr(row, "world_name"),
        Datacenter:    GetStr(row, "datacenter") ?? string.Empty,
        Region:        GetStr(row, "region") ?? string.Empty,
        RankingAlltime: GetLong(row, "ranking_alltime"),
        Ranking1h:      GetLong(row, "ranking_1h"),
        Ranking3h:      GetLong(row, "ranking_3h"),
        Ranking6h:      GetLong(row, "ranking_6h"),
        Ranking12h:     GetLong(row, "ranking_12h"),
        Ranking1d:      GetLong(row, "ranking_1d"),
        Ranking3d:      GetLong(row, "ranking_3d"),
        Ranking7d:      GetLong(row, "ranking_7d"),
        UpdatedAt:      GetNullableLong(row, "updated_at"),
        LastSaleTime:   GetNullableLong(row, "last_sale_time"));

    private static bool HasColumn(Row row, string name) => row.GetColumn(name) is not null;

    private static string? GetStr(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<string>(name) : null;

    private static long GetLong(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<long>(name) : 0L;

    private static long? GetNullableLong(Row row, string name) =>
        HasColumn(row, name) && !row.IsNull(name) ? row.GetValue<long>(name) : null;
}
