using Ffmt.Core.Models;

namespace Ffmt.Core.Quarantine;

public readonly record struct PricePoint(bool Hq, int UnitPrice);

public sealed record PriceBaseline(long MedianUnitPrice, int SampleCount, DateTimeOffset ComputedAt);

public sealed record QuarantinedSale(Sale Sale, string Reason, long BaselineMedian, DateTimeOffset QuarantinedAt);

// NoBaseline is the subset of Accepted that was let through unevaluated, so callers can meter the fail-open gap.
public sealed record AnomalyPartition(
    IReadOnlyList<Sale> Accepted,
    IReadOnlyList<QuarantinedSale> Quarantined,
    IReadOnlyList<Sale> NoBaseline);

public static class QuarantineReasons
{
    public const string UnitPriceDeviation = "unit_price_deviation";
}
