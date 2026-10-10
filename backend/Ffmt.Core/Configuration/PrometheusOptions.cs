namespace Ffmt.Core.Configuration;

public sealed class PrometheusOptions
{
    public const string SectionName = "Prometheus";

    public string BaseUrl { get; init; } = "http://ffmt_prometheus:9090/";
    public int RequestTimeoutSeconds { get; init; } = 5;
}
