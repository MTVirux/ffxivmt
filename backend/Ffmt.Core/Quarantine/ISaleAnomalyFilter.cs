using Ffmt.Core.Models;

namespace Ffmt.Core.Quarantine;

public interface ISaleAnomalyFilter
{
    Task<AnomalyPartition> PartitionAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);
}
