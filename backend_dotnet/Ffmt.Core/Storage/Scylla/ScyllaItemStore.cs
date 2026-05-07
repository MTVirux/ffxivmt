using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaItemStore(IScyllaSession scylla) : IItemStore
{
    private const string CqlGetById = "SELECT id, name, marketable, craftable FROM items WHERE id = ?";
    private const string CqlGetAllNames = "SELECT id, name FROM items";
    private const string CqlGetMarketableIds = "SELECT id FROM items WHERE marketable = true ALLOW FILTERING";
    private const string CqlGetCraftableIds = "SELECT id FROM items WHERE craftable = true ALLOW FILTERING";

    public async Task<Item?> GetAsync(int id, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetById, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(id)).ConfigureAwait(false);
        var row = rows.FirstOrDefault();
        return row is null ? null : MapRow(row);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetAllNamesAsync(CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetAllNames, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var result = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            result[row.GetValue<int>("id")] = row.GetValue<string>("name") ?? string.Empty;
        }
        return result;
    }

    public Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default) =>
        FetchIdsAsync(CqlGetMarketableIds, ct);

    public Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default) =>
        FetchIdsAsync(CqlGetCraftableIds, ct);

    private async Task<IReadOnlyList<int>> FetchIdsAsync(string cql, CancellationToken ct)
    {
        var stmt = await scylla.PrepareAsync(cql, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var result = new List<int>();
        foreach (var row in rows)
        {
            result.Add(row.GetValue<int>("id"));
        }
        return result;
    }

    private static Item MapRow(Row row) => new(
        row.GetValue<int>("id"),
        row.GetValue<string>("name") ?? string.Empty,
        SafeBool(row, "marketable"),
        SafeBool(row, "craftable"));

    private static bool SafeBool(Row row, string column) =>
        !row.IsNull(column) && row.GetValue<bool>(column);
}
