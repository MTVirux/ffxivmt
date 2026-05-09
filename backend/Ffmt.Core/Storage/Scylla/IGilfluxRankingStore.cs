using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IGilfluxRankingStore
{
    Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default);
    Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default);
    Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default);
}
