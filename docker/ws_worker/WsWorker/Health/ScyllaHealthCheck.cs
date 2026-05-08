using Cassandra;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WsWorker.Services;

namespace WsWorker.Health;

public sealed class ScyllaHealthCheck : IHealthCheck
{
    private readonly ScyllaService _scyllaService;

    public ScyllaHealthCheck(ScyllaService scyllaService)
        => _scyllaService = scyllaService;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _scyllaService.ExecuteAsync(new SimpleStatement("SELECT now() FROM system.local"));
            return HealthCheckResult.Healthy();
        }
        catch (InvalidOperationException ex)
        {
            return HealthCheckResult.Unhealthy("Scylla not yet initialized", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Scylla unreachable", ex);
        }
    }
}
