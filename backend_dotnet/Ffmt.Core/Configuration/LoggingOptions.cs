namespace Ffmt.Core.Configuration;

public sealed class LoggingOptions
{
    public const string SectionName = "FfmtLogging";

    public string LogDirectory { get; init; } = "logs";
    public string[] ChannelsEnabled { get; init; } = ["ERROR"];
    public bool MirrorToAllLog { get; init; } = true;
    public int FileRetainedFileCount { get; init; } = 14;
    public long FileRollingSizeBytes { get; init; } = 100L * 1024 * 1024;
}
