using Cassandra;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>
/// Singleton wrapper around a connected DataStax <see cref="ISession"/>.
/// Connection is established lazily on first access so the API can start even when Scylla is unreachable
/// — readiness will report unhealthy until the cluster comes up, but liveness is not affected.
/// </summary>
public interface IScyllaSession
{
    ISession Session { get; }

    Task<PreparedStatement> PrepareAsync(string cql, CancellationToken ct = default);
}
