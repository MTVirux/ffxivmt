namespace Ffmt.Core.Configuration;

public sealed class ScyllaOptions
{
    public const string SectionName = "Scylla";

    // Empty, not a seeded default: the configuration binder appends bound values to whatever the
    // array already holds. appsettings.json supplies the single-host value; the app VM override
    // replaces index 0 with the private IP.
    public string[] ContactPoints { get; init; } = [];
    public int Port { get; init; } = 9042;
    public string Keyspace { get; init; } = "ffmt";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int QueryTimeoutMillis { get; init; } = 12000;
}
