using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface ISaleStore
{
    /// <summary>
    /// Inserts <paramref name="sales"/> in 1000-row UNLOGGED batches. Intermediate batches use
    /// CL=ONE for throughput; the trailing partial batch escalates to CL=ALL to match the
    /// legacy PHP <c>Sale_model::add_sale</c> 1/5 pair.
    /// </summary>
    Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);

    /// <summary>
    /// Returns sales whose <c>buyer_name</c> column matches <paramref name="buyerName"/>, optionally
    /// further filtered by <paramref name="worldId"/>. Hits the <c>sales_buyers</c> secondary index;
    /// the world filter requires <c>ALLOW FILTERING</c> like the legacy query.
    /// </summary>
    Task<IReadOnlyList<Sale>> SearchBuyerAsync(string buyerName, int? worldId, CancellationToken ct = default);
}
