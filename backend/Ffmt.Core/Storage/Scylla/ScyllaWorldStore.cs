using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaWorldStore(IScyllaSession scylla) : IWorldStore
{
    private const string CqlGetAll = "SELECT id, name, datacenter, region FROM worlds";
    private const string CqlGetById = "SELECT id, name, datacenter, region FROM worlds WHERE id = ?";
    private const string CqlUpsert = "INSERT INTO worlds (id, name, datacenter, region) VALUES (?, ?, ?, ?)";

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
        // worlds is ~80 rows; an in-memory scan over GetAllAsync is cheaper than a SI lookup.
        var all = await GetAllAsync(ct).ConfigureAwait(false);
        return all.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(World world, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlUpsert, ct).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(stmt.Bind(world.Id, world.Name, world.Datacenter, world.Region)).ConfigureAwait(false);
    }

    private static World MapRow(Row row) => new(
        row.GetValue<int>("id"),
        row.GetValue<string>("name") ?? string.Empty,
        row.GetValue<string>("datacenter") ?? string.Empty,
        row.GetValue<string>("region") ?? string.Empty);
}
