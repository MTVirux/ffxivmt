using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>The ingest chokepoint - DI resolves it to the quarantine-filtering decorator. New
/// ingest code takes this, not ISaleStore, which is the raw store for reads and maintenance.</summary>
public interface ISaleWriter
{
    Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);
}
