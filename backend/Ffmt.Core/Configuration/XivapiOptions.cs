namespace Ffmt.Core.Configuration;

public sealed class XivapiOptions
{
    public const string SectionName = "Xivapi";

    public string BaseUrl { get; init; } = "https://v2.xivapi.com/api/";
    public int MaxRetries { get; init; } = 5;
    public double InitialBackoffSeconds { get; init; } = 0.5;
    public double MaxBackoffSeconds { get; init; } = 10.0;
    public int RequestTimeoutSeconds { get; init; } = 60;
}
