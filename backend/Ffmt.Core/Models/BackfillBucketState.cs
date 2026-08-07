namespace Ffmt.Core.Models;

/// <summary>
/// Import progress for one slice of the marketable catalogue. Each bucket advances on its own,
/// so a request that keeps failing stalls only its own items.
/// </summary>
public sealed record BackfillBucketState(
    string Region,
    string Loop,
    int Bucket,
    DateTimeOffset? LastImportAt,
    DateTimeOffset? EarliestImportAt,
    bool CrawlComplete);

public static class BackfillLoops
{
    public const string Live = "live";
    public const string Historical = "historical";
}
