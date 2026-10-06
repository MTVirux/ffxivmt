using Ffmt.Core.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace WsWorker.Workers;

public sealed class SaleWriteWatchdog : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(5);

    private readonly WriteStallTracker _tracker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SaleWriteWatchdog> _logger;

    public SaleWriteWatchdog(
        WriteStallTracker tracker,
        IHostApplicationLifetime lifetime,
        ILogger<SaleWriteWatchdog> logger)
    {
        _tracker = tracker;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_tracker.IsStalled(DateTimeOffset.UtcNow, StallThreshold))
                continue;

            _logger.LogCritical(
                "No successful Scylla sale write for {Minutes} min while inserts keep failing - stopping so the container restarts",
                StallThreshold.TotalMinutes);
            // The driver can stay wedged with the host marked down even after Scylla recovers,
            // so only a fresh process helps - compose's restart: always brings one up.
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
            return;
        }
    }
}
