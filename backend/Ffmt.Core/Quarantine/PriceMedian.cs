namespace Ffmt.Core.Quarantine;

public static class PriceMedian
{
    // Lower median: integer-safe and always an observed price. Tolerates up to 50% contamination,
    // so a baseline can be built from sales that still contain the trades it is meant to catch.
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
