using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>The ingest write path, resolved to the quarantine-filtering decorator. Sale writes go
/// through this; ISaleStore resolves to the raw store and is for reads and maintenance.</summary>
public interface ISaleWriter
{
    Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);
}
