using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>
/// Orders a loop's regions so the one that advanced longest ago runs first. Every restart begins a new
/// pass, so a fixed order kept re-running the first region and starved the rest. An interrupted region
/// has just written its buckets, so the next pass starts elsewhere without any extra state.
/// </summary>
public static class BackfillRegionOrder
{
    /// <summary>A region with no written bucket has never advanced and goes first; ties keep the given order.</summary>
    public static IReadOnlyList<string> StalestFirst(
        IEnumerable<(string Region, IReadOnlyList<BackfillBucketProgress> Buckets)> regions) =>
        regions
            .OrderBy(r => r.Buckets.Max(b => b.PointerWrittenAt) ?? DateTimeOffset.MinValue)
            .Select(r => r.Region)
            .ToList();
}
