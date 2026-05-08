using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IGilfluxRankingStore
{
    Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByDatacenterAsync(string datacenter, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByRegionAsync(string region, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default);

    /// <summary>Recomputes the rolling-window rankings for one (world, item) pair from the sales table.</summary>
    Task UpdateRankingAsync(int worldId, int itemId, CancellationToken ct = default);

    Task<int?> GetOneItemIdWithMissingNameAsync(CancellationToken ct = default);
}
