using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaWorldStore(IScyllaSession scylla) : IWorldStore
{
    private const string CqlGetAll = "SELECT id, name, datacenter, region FROM worlds";
    private const string CqlGetById = "SELECT id, name, datacenter, region FROM worlds WHERE id = ?";
    private const string CqlGetByName = "SELECT id, name, datacenter, region FROM worlds WHERE name = ? ALLOW FILTERING";

    public async Task<IReadOnlyList<World>> GetAllAsync(CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetAll, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var result = new List<World>();
        foreach (var row in rows)
        {
            result.Add(MapRow(row));
        }
        return result;
    }

    public async Task<World?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetById, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(id)).ConfigureAwait(false);
        var row = rows.FirstOrDefault();
        return row is null ? null : MapRow(row);
    }

    public async Task<World?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetByName, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(name)).ConfigureAwait(false);
        var row = rows.FirstOrDefault();
        return row is null ? null : MapRow(row);
    }

    private static World MapRow(Row row) => new(
        row.GetValue<int>("id"),
        row.GetValue<string>("name") ?? string.Empty,
        row.GetValue<string>("datacenter") ?? string.Empty,
        row.GetValue<string>("region") ?? string.Empty);
}
