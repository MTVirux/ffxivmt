namespace Ffmt.Core.Configuration;

public sealed class ScyllaOptions
{
    public const string SectionName = "Scylla";

    // Empty, not a seeded default: the config binder appends bound values to whatever the array
    // already holds, so any default here survives alongside the configured contact points.
    public string[] ContactPoints { get; init; } = [];
    public int Port { get; init; } = 9042;
    public string Keyspace { get; init; } = "ffmt";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int QueryTimeoutMillis { get; init; } = 12000;
}
