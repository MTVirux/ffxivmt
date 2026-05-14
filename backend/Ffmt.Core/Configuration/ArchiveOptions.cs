namespace Ffmt.Core.Configuration;

public sealed class ArchiveOptions
{
    public const string SectionName = "Archive";

    public string Endpoint { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public int ExportConcurrency { get; init; } = 16;
    public int LookbackDays { get; init; } = 90;
}
