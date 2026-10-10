namespace Ffmt.Core.Status;

public sealed record BackfillProgressResponse(
    bool Available,
    long GeneratedAt,
    IReadOnlyList<RegionBackfillProgress> Regions);

public sealed record RegionBackfillProgress(
    string Region,
    int BucketsTotal,
    int BucketsComplete,
    long? ReachedBackTo,
    long? SlowestBucketAt,
    long? LastAdvancedAt);
