namespace WsWorker.Options;

public sealed class BackfillOptions
{
    public int ChunkDays { get; set; } = 7;
    public int LiveGapIntervalMinutes { get; set; } = 15;
    public int HistoricalCrawlIntervalMinutes { get; set; } = 60;
    public int SkipIfGapUnderMinutes { get; set; } = 5;
}
