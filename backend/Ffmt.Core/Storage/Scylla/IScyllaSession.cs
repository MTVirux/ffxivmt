using Cassandra;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>Connection is established lazily so the API can start even when Scylla is unreachable.</summary>
public interface IScyllaSession
{
    ISession Session { get; }

    Task<PreparedStatement> PrepareAsync(string cql, CancellationToken ct = default);
}
