using Microsoft.Extensions.Diagnostics.HealthChecks;
using WsWorker.Workers;

namespace WsWorker.Health;

public sealed class WsConsumerHealthCheck : IHealthCheck
{
    private readonly UniversalisWsConsumer _consumer;

    public WsConsumerHealthCheck(UniversalisWsConsumer consumer)
        => _consumer = consumer;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _consumer.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("WebSocket not connected");

        return Task.FromResult(result);
    }
}
