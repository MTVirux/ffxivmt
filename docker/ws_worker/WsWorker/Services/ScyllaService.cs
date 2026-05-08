using Cassandra;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using WsWorker.Options;

using CassandraSession = Cassandra.ISession;
using CassandraCluster = Cassandra.ICluster;

namespace WsWorker.Services;

public sealed class ScyllaService
{
    private readonly ScyllaOptions _options;
    private readonly ILogger<ScyllaService> _logger;

    private CassandraCluster? _cluster;
    private CassandraSession? _session;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _statementCache = new();

    public PreparedStatement SalesInsert { get; private set; } = null!;

    private const string SalesInsertCql =
        "INSERT INTO ffmt.sales " +
        "(buyer_name, hq, on_mannequin, unit_price, quantity, sale_time, world_id, item_id, world_name, item_name, datacenter, region, total) " +
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

    public ScyllaService(IOptions<ScyllaOptions> options, ILogger<ScyllaService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            _cluster = Cluster.Builder()
                .AddContactPoint(_options.Host)
                .WithDefaultKeyspace(_options.Keyspace)
                .Build();

            _session = await _cluster.ConnectAsync(_options.Keyspace);
            _logger.LogInformation("Connected to Scylla at {Host}, keyspace {Keyspace}", _options.Host, _options.Keyspace);

            SalesInsert = await _statementCache.GetOrAdd(SalesInsertCql, c => Session.PrepareAsync(c));
            _logger.LogInformation("Pre-prepared sales INSERT statement");

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private CassandraSession Session
    {
        get
        {
            if (_session is null)
                throw new InvalidOperationException("ScyllaService has not been initialized. Call InitializeAsync() first.");
            return _session;
        }
    }

    public Task<PreparedStatement> PrepareAsync(string cql) =>
        _statementCache.GetOrAdd(cql, c => Session.PrepareAsync(c));

    public Task<RowSet> ExecuteAsync(IStatement statement)
        => Session.ExecuteAsync(statement);

    public void ExecuteAsyncFireAndForget(IStatement statement, ILogger logger)
    {
        Session.ExecuteAsync(statement).ContinueWith(
            t => logger.LogError(t.Exception, "Scylla fire-and-forget execution failed"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    internal CassandraSession GetSession() => Session;

    public void Shutdown()
    {
        _session?.Dispose();
        _cluster?.Dispose();
        _logger.LogInformation("Scylla session and cluster shut down");
    }
}
