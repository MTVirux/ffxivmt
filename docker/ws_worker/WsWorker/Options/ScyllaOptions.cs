namespace WsWorker.Options;

public sealed class ScyllaOptions
{
    public string Host { get; set; } = "10.0.0.3";
    public string Keyspace { get; set; } = "ffmt";
}
