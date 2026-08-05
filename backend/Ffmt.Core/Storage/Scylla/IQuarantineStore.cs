using Ffmt.Core.Quarantine;

namespace Ffmt.Core.Storage.Scylla;

public interface IQuarantineStore
{
    Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default);
}
