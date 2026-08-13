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

/// <summary>
/// Window arithmetic for the Universalis history endpoint, which only accepts a window
/// relative to now.
/// </summary>
public static class BackfillWindow
{
    public static DateTimeOffset HistoricalStart(DateTimeOffset earliestImportAt, int chunkDays) =>
        earliestImportAt - TimeSpan.FromDays(chunkDays);

    /// <summary>
    /// Everything since <paramref name="windowStart"/>, not the window's own width - a crawl
    /// walking backwards has to ask for the whole span and discard the newer part.
    /// </summary>
    public static long EntriesWithinSeconds(DateTimeOffset windowStart, DateTimeOffset now) =>
        (long)(now - windowStart).TotalSeconds;
}
