namespace Ffmt.Core.Quarantine;

public static class PriceMedian
{
    /// <summary>
    /// Lower median: sorts ascending and takes index (n-1)/2. Deterministic, integer-safe, and
    /// always an actually-observed price. Tolerates up to 50% contamination, which is what lets a
    /// baseline be computed from sales that still contain the trades it is meant to catch.
    /// </summary>
    public static long Compute(IReadOnlyList<int> unitPrices)
    {
        if (unitPrices.Count == 0)
        {
            return 0L;
        }

        var sorted = unitPrices.ToArray();
        Array.Sort(sorted);
        return sorted[(sorted.Length - 1) / 2];
    }
}
