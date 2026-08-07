using Ffmt.Core.External;

namespace Ffmt.Core.Configuration;

public sealed class BackfillOptions
{
    public const string SectionName = "Backfill";

    public int ChunkDays { get; set; } = 7;
    public int LiveGapIntervalMinutes { get; set; } = 15;
    public int HistoricalCrawlIntervalMinutes { get; set; } = 60;
    public int SkipIfGapUnderMinutes { get; set; } = 5;
    public BackfillTuning Tuning { get; set; } = new();
}
