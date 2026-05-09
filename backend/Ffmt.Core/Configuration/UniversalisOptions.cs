namespace Ffmt.Core.Configuration;

public sealed class UniversalisOptions
{
    public const string SectionName = "Universalis";

    public string BaseUrl { get; init; } = "https://universalis.app/api/v2/";
    public string WsUrl { get; init; } = "wss://universalis.app/api/ws";

    public int MaxRetries { get; init; } = 20;
    public double InitialBackoffSeconds { get; init; } = 0.2;
    public double MaxBackoffSeconds { get; init; } = 10.0;
    public int RequestTimeoutSeconds { get; init; } = 30;

    public string[] RegionsToUse { get; init; } = ["Europe", "North-America"];
    public string[] RegionsToImport { get; init; } = ["Europe"];
    public int ItemsPerRequest { get; init; } = 100;
    public int MaxRequestsPerSecond { get; init; } = 25;
    public int MaxRequestsPerSecondBurst { get; init; } = 50;
}
