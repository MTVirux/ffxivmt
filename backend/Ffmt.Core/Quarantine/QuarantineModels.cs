using Ffmt.Core.Models;

namespace Ffmt.Core.Quarantine;

public readonly record struct PricePoint(bool Hq, int UnitPrice);

public sealed record PriceBaseline(long MedianUnitPrice, int SampleCount, DateTimeOffset ComputedAt);

public sealed record QuarantinedSale(Sale Sale, string Reason, long BaselineMedian, DateTimeOffset QuarantinedAt);

/// <summary><paramref name="NoBaseline"/> is the subset of <paramref name="Accepted"/> that was
/// accepted without evaluation, so the caller can meter the fail-open gap.</summary>
public sealed record AnomalyPartition(
    IReadOnlyList<Sale> Accepted,
    IReadOnlyList<QuarantinedSale> Quarantined,
    IReadOnlyList<Sale> NoBaseline);

public static class QuarantineReasons
{
    public const string UnitPriceDeviation = "unit_price_deviation";
}
