namespace Ffmt.Core.External;

/// <summary>
/// Splits the marketable catalogue into stable buckets, each about one Universalis request.
/// Progress is tracked per bucket so one failing request stalls only its own items instead of
/// holding up the whole pass.
/// </summary>
public static class BackfillBuckets
{
    public static int BucketCountFor(int itemCount, int itemsPerRequest) =>
        Math.Max(1, (int)Math.Ceiling(itemCount / (double)itemsPerRequest));

    public static int BucketFor(int itemId, int bucketCount) =>
        Math.Abs(itemId % bucketCount);

    public static IReadOnlyDictionary<int, IReadOnlyList<int>> Group(
        IReadOnlyList<int> itemIds, int bucketCount)
    {
        var grouped = new Dictionary<int, List<int>>();
        foreach (var id in itemIds)
        {
            var bucket = BucketFor(id, bucketCount);
            if (!grouped.TryGetValue(bucket, out var members))
            {
                members = [];
                grouped[bucket] = members;
            }

            members.Add(id);
        }

        return grouped.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value);
    }
}

/// <summary>Window arithmetic for the Universalis history endpoint.</summary>
public static class BackfillWindow
{
    public static DateTimeOffset HistoricalStart(DateTimeOffset earliestImportAt, int chunkDays) =>
        earliestImportAt - TimeSpan.FromDays(chunkDays);

    /// <summary>
    /// <c>entriesWithin</c> counts back from <c>entriesUntil</c>, both in whole seconds. The bounds are
    /// widened outwards so a sale on a boundary second lands in at least one window - rows are keyed by
    /// sale, so overlap is harmless and a gap is not.
    /// </summary>
    public static (long EntriesWithin, long EntriesUntil) ToHistoryQuery(BackfillBucketWindow window)
    {
        var until = window.End.ToUnixTimeSeconds() + 1;
        return (until - window.Start.ToUnixTimeSeconds(), until);
    }
}
