using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Fakes;

/// <summary>Inert <see cref="ISaleStore"/>; tests derive from it and override only the members
/// they exercise.</summary>
internal class FakeSaleStore : ISaleStore
{
    public virtual Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default) =>
        Task.FromResult(new SaleBatchResult(sales.Count, 0d));

    public virtual Task<IReadOnlyList<Sale>> SearchBuyerAsync(string buyerName, int? worldId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Sale>>([]);

    public virtual Task<IReadOnlyList<Sale>> GetByItemAndWorldAsync(int itemId, int worldId, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Sale>>([]);

    public virtual Task<IReadOnlyList<Sale>> GetByItemAndWorldInRangeAsync(int itemId, int worldId, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Sale>>([]);

    public virtual Task DeleteByItemAndWorldInRangeAsync(int itemId, int worldId, DateOnly date, IReadOnlyList<Sale> sales, CancellationToken ct = default) =>
        Task.CompletedTask;

    public virtual Task<IReadOnlyList<PricePoint>> GetPricePointsSinceAsync(int itemId, int worldId, DateTimeOffset since, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PricePoint>>([]);

    public virtual Task DeleteExactAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default) =>
        Task.CompletedTask;

    public virtual Task BackfillTotalPriceAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default) =>
        Task.CompletedTask;
}
