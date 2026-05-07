namespace Ffmt.Core.Configuration;

public sealed class ElasticOptions
{
    public const string SectionName = "Elastic";

    public string Url { get; init; } = "http://ffmt_elastic:9200";
    public string ItemsIndex { get; init; } = "items";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 10;
}
