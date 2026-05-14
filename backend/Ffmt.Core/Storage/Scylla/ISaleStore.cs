using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface ISaleStore
{
    Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);

    Task<IReadOnlyList<Sale>> SearchBuyerAsync(string buyerName, int? worldId, CancellationToken ct = default);

    /// <summary>World is required; fan-out across all worlds would need ALLOW FILTERING and scale poorly.</summary>
    Task<IReadOnlyList<Sale>> GetByItemAndWorldAsync(int itemId, int worldId, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<Sale>> GetByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, CancellationToken ct = default);

    Task DeleteByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, IReadOnlyList<Sale> sales, CancellationToken ct = default);
}
