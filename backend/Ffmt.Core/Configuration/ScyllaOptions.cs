namespace Ffmt.Core.Configuration;

public sealed class ScyllaOptions
{
    public const string SectionName = "Scylla";

    public string[] ContactPoints { get; init; } = ["ffmt_scylla_node"];
    public int Port { get; init; } = 9042;
    public string Keyspace { get; init; } = "ffmt";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int QueryTimeoutMillis { get; init; } = 12000;
}
