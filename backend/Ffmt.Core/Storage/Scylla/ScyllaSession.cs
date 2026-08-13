using Cassandra;
using Ffmt.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaSession : IScyllaSession, IDisposable
{
    private readonly Lazy<(Cluster Cluster, ISession Session)> _state;

    public ScyllaSession(IOptions<ScyllaOptions> options, ILogger<ScyllaSession> logger)
    {
        var opts = options.Value;

        _state = new Lazy<(Cluster, ISession)>(
            () =>
            {
                var builder = Cluster.Builder()
                    .AddContactPoints(opts.ContactPoints)
                    .WithPort(opts.Port)
                    .WithLoadBalancingPolicy(new TokenAwarePolicy(new RoundRobinPolicy()))
                    .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.LocalOne))
                    .WithSocketOptions(new SocketOptions().SetReadTimeoutMillis(opts.QueryTimeoutMillis))
                    // Retry against another replica every 400 ms, twice - 3 requests in flight max.
                    .WithSpeculativeExecutionPolicy(new ConstantSpeculativeExecutionPolicy(400, 2));

                if (!string.IsNullOrEmpty(opts.Username))
                {
                    builder = builder.WithCredentials(opts.Username, opts.Password ?? string.Empty);
                }

                var cluster = builder.Build();
                var session = cluster.Connect(opts.Keyspace);

                logger.LogInformation(
                    "Connected to Scylla {Hosts}:{Port} keyspace={Keyspace}",
                    string.Join(",", opts.ContactPoints), opts.Port, opts.Keyspace);

                return (cluster, session);
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ISession Session => _state.Value.Session;

    public Task<PreparedStatement> PrepareAsync(string cql, CancellationToken ct = default)
    {
        // The driver caches prepared statements per cluster by CQL text, so repeats are lookups.
        return Session.PrepareAsync(cql);
    }

    public void Dispose()
    {
        if (!_state.IsValueCreated)
        {
            return;
        }

        var (cluster, session) = _state.Value;
        session.Dispose();
        cluster.Dispose();
    }
}
