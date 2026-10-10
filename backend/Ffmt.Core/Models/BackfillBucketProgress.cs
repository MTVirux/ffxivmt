namespace Ffmt.Core.Models;

/// <summary>A bucket's crawl position plus when its row was last written, which is when its
/// pointer last moved since the crawl only upserts a bucket after advancing it.</summary>
public sealed record BackfillBucketProgress(
    int Bucket,
    DateTimeOffset? EarliestImportAt,
    bool CrawlComplete,
    DateTimeOffset? PointerWrittenAt);
