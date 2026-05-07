using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IWorldStore
{
    Task<IReadOnlyList<World>> GetAllAsync(CancellationToken ct = default);

    Task<World?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<World?> GetByNameAsync(string name, CancellationToken ct = default);
}
