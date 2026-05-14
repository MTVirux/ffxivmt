using Cassandra;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaArchiveStore(IScyllaSession scylla) : IArchiveStore
{
    private const string CqlSelect =
        "SELECT exported_at FROM ffmt.archive_export_state WHERE world_id = ? AND export_date = ?";

    private const string CqlInsert =
        "INSERT INTO ffmt.archive_export_state (world_id, export_date, exported_at) VALUES (?, ?, ?)";

    public async Task<bool> IsExportedAsync(int worldId, DateOnly date, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlSelect, ct).ConfigureAwait(false);
        var localDate = new LocalDate(date.Year, date.Month, date.Day);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(worldId, localDate)).ConfigureAwait(false);
        return rows.FirstOrDefault() is not null;
    }

    public async Task MarkExportedAsync(int worldId, DateOnly date, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlInsert, ct).ConfigureAwait(false);
        var localDate = new LocalDate(date.Year, date.Month, date.Day);
        await scylla.Session.ExecuteAsync(stmt.Bind(worldId, localDate, DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }
}
