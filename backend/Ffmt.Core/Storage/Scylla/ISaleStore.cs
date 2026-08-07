using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;

namespace Ffmt.Core.Storage.Scylla;

public interface ISaleStore : ISaleWriter
{
    Task<IReadOnlyList<Sale>> SearchBuyerAsync(string buyerName, int? worldId, CancellationToken ct = default);

    /// <summary>World is required; fan-out across all worlds would need ALLOW FILTERING and scale poorly.</summary>
    Task<IReadOnlyList<Sale>> GetByItemAndWorldAsync(int itemId, int worldId, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<Sale>> GetByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, CancellationToken ct = default);

    Task DeleteByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, IReadOnlyList<Sale> sales, CancellationToken ct = default);

    /// <summary>Narrow projection for the baseline job, which touches millions of partitions.</summary>
    Task<IReadOnlyList<PricePoint>> GetPricePointsSinceAsync(
        int itemId, int worldId, DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Deletes exactly these rows from sales and sales_by_buyer. Unlike
    /// DeleteByItemAndWorldInRangeAsync this does not take neighbouring sales with it.</summary>
    Task DeleteExactAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);

    Task BackfillTotalPriceAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);
}
