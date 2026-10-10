namespace Ffmt.Core.Mannequin;

public static class MannequinCap
{
    /// <summary>Oldest days to drop so the estimate fits under the cap. The newest day always
    /// survives, even alone over the cap - an empty feed is worse than overshooting.</summary>
    public static IReadOnlyList<DateOnly> DaysToDrop(
        IReadOnlyDictionary<DateOnly, long> rowsPerDay, long bytesPerRow, long maxBytes)
    {
        var total = rowsPerDay.Values.Sum() * bytesPerRow;
        var ordered = rowsPerDay.Keys.Order().ToList();
        var drop = new List<DateOnly>();

        for (var i = 0; i < ordered.Count - 1 && total > maxBytes; i++)
        {
            drop.Add(ordered[i]);
            total -= rowsPerDay[ordered[i]] * bytesPerRow;
        }

        return drop;
    }
}
