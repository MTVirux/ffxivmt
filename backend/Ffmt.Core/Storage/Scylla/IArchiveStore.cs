namespace Ffmt.Core.Storage.Scylla;

public interface IArchiveStore
{
    Task<bool> IsExportedAsync(int worldId, DateOnly date, CancellationToken ct = default);
    Task MarkExportedAsync(int worldId, DateOnly date, CancellationToken ct = default);
}
