namespace Ffmt.Core.Models;

/// <summary>Import progress for one slice of the marketable catalogue.</summary>
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
