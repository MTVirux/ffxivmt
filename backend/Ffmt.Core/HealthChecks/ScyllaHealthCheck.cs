using Cassandra;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ffmt.Core.HealthChecks;

public sealed class ScyllaHealthCheck(IScyllaSession scylla) : IHealthCheck
{
    private static readonly IStatement Probe =
        new SimpleStatement("SELECT now() FROM system.local").SetConsistencyLevel(ConsistencyLevel.LocalOne);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await scylla.Session.ExecuteAsync(Probe).ConfigureAwait(false);
            _ = rows.FirstOrDefault();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
