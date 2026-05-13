using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaGilfluxRankingStore(IScyllaSession scylla) : IGilfluxRankingStore
{
    private const string CqlByWorld = """
        SELECT world_id, item_id, rankings, last_sale_time, updated_at
        FROM gilflux_rankings
        WHERE world_id = ?
        """;

    // item_id is a clustering column; ALLOW FILTERING scans ~80 world partitions (bounded).
    private const string CqlByItem = """
        SELECT world_id, item_id, rankings, last_sale_time, updated_at
        FROM gilflux_rankings
        WHERE item_id = ?
        ALLOW FILTERING
        """;

    private const string CqlByItemAndWorld = """
        SELECT world_id, item_id, rankings, last_sale_time, updated_at
        FROM gilflux_rankings
        WHERE world_id = ? AND item_id = ?
        """;

    private readonly RequestCoalescer<int, IReadOnlyList<GilfluxRanking>> _worldCoalescer = new();
    private readonly RequestCoalescer<int, IReadOnlyList<GilfluxRanking>> _itemCoalescer = new();
    private readonly RequestCoalescer<(int, int), IReadOnlyList<GilfluxRanking>> _itemWorldCoalescer = new();

    public Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default) =>
        _worldCoalescer.CoalesceAsync(worldId, async () =>
        {
            var stmt = await scylla.PrepareAsync(CqlByWorld).ConfigureAwait(false);
            return await ExecuteAndMapAsync(stmt.Bind(worldId)).ConfigureAwait(false);
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
            return await ExecuteAndMapAsync(stmt.Bind(worldId, itemId)).ConfigureAwait(false);
        });

    private async Task<IReadOnlyList<GilfluxRanking>> ExecuteAndMapAsync(IStatement stmt)
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
        WorldId:      !row.IsNull("world_id") ? row.GetValue<int>("world_id") : (int?)null,
        Rankings:     GetRankingsMap(row),
        UpdatedAt:    GetNullableEpochMs(row, "updated_at"),
        LastSaleTime: GetNullableEpochMs(row, "last_sale_time"));

    private static IReadOnlyDictionary<string, long> GetRankingsMap(Row row)
    {
        if (row.IsNull("rankings"))
            return new Dictionary<string, long>();
        var raw = row.GetValue<IDictionary<string, long>>("rankings");
        return raw as IReadOnlyDictionary<string, long> ?? new Dictionary<string, long>(raw);
    }

    private static long? GetNullableEpochMs(Row row, string name) =>
        !row.IsNull(name) ? row.GetValue<DateTimeOffset>(name).ToUnixTimeMilliseconds() : null;
}
