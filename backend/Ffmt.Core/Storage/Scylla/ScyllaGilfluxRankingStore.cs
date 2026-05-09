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

    // WARNING: full-table scatter — item_id is only the first half of the composite PK
    // ((item_id, world_id)), so this scans every (item_id, world_id) partition for the
    // matching item across all worlds. OK for now (rarely-hit path); revisit with a
    // gilflux_by_item companion if telemetry shows it matters.
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
