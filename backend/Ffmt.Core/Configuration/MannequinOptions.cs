namespace Ffmt.Core.Configuration;

public sealed class MannequinOptions
{
    public const string SectionName = "Mannequin";

    public int MaxSizeMb { get; init; } = 1024;

    // Estimated, not measured: Scylla's REST API is localhost-only on the Scylla VM.
    public int EstimatedBytesPerRow { get; init; } = 100;

    public int CleanupIntervalHours { get; init; } = 6;
    public int CountConcurrency { get; init; } = 16;
    public int MaxDaysPerRequest { get; init; } = 7;

    // Cursor pages are uncached, so this bounds what one request can make Scylla read.
    // A region spans up to 32 worlds, which gets 2 days per request.
    public int MaxPartitionReadsPerRequest { get; init; } = 64;

    public int FeedCacheSeconds { get; init; } = 20;
}
