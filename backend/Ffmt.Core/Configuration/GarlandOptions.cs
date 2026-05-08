namespace Ffmt.Core.Configuration;

public sealed class GarlandOptions
{
    public const string SectionName = "Garland";

    public string BaseUrl { get; init; } = "https://www.garlandtools.org/db/doc/";
    public int MaxRetries { get; init; } = 20;
    public double InitialBackoffSeconds { get; init; } = 0.2;
    public double MaxBackoffSeconds { get; init; } = 10.0;
    public int RequestTimeoutSeconds { get; init; } = 30;
}
