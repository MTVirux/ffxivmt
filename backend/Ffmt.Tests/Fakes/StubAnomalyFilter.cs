using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;

namespace Ffmt.Tests.Fakes;

/// <summary>Quarantines anything at or above a million gil per unit, so callers can pick an
/// obviously-anomalous price without configuring baselines.</summary>
internal sealed class StubAnomalyFilter : ISaleAnomalyFilter
{
    public Task<AnomalyPartition> PartitionAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default) =>
        Task.FromResult(new AnomalyPartition(
            sales.Where(s => s.UnitPrice < 1_000_000).ToList(),
            sales.Where(s => s.UnitPrice >= 1_000_000)
                 .Select(s => new QuarantinedSale(s, QuarantineReasons.UnitPriceDeviation, 500, DateTimeOffset.UnixEpoch))
                 .ToList(),
            []));
}
