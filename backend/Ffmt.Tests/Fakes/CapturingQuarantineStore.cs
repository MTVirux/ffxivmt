using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Fakes;

internal sealed class CapturingQuarantineStore : IQuarantineStore
{
    public List<QuarantinedSale> Written { get; } = [];

    public Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default)
    {
        Written.AddRange(sales);
        return Task.CompletedTask;
    }
}
