using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed class DeferredSweepWorker : BackgroundService
{
    private readonly IDirtyPairQueue _queue;
    private readonly IRankingRefresher _refresher;
    private readonly GilfluxOptions _options;
    private readonly ILogger<DeferredSweepWorker> _logger;

    public DeferredSweepWorker(
        IDirtyPairQueue queue,
        IRankingRefresher refresher,
        IOptions<GilfluxOptions> options,
        ILogger<DeferredSweepWorker> logger)
    {
        _queue = queue;
        _refresher = refresher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var pollDelay = TimeSpan.FromSeconds(Math.Max(1, _options.DeferredSweepPollSeconds));
        var batchSize = Math.Max(1, _options.DeferredSweepClaimBatch);
        var concurrency = Math.Max(1, _options.DeferredSweepConcurrency);

        _logger.LogInformation(
            "DeferredSweepWorker started — poll={PollDelay}s batch={Batch} concurrency={Concurrency}",
            pollDelay.TotalSeconds, batchSize, concurrency);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var claims = await _queue.ClaimBatchAsync(batchSize, ct).ConfigureAwait(false);
                if (claims.Count == 0)
                {
                    await Task.Delay(pollDelay, ct).ConfigureAwait(false);
                    continue;
                }

                var pairs = claims.Select(c => (c.WorldId, c.ItemId)).ToList();
                await _refresher.RefreshManyAsync(pairs, concurrency, ct).ConfigureAwait(false);

                // Remove all claims regardless of individual refresh success — RefreshManyAsync
                // logs and swallows per-pair failures, and the dirty queue is opportunistic
                // (the *next* sale on a failed pair re-enqueues it via the live coalescer).
                await _queue.RemoveAsync(claims, ct).ConfigureAwait(false);

                MetricsCatalog.DirtyPairsDrainedTotal.Inc(claims.Count);
                _logger.LogDebug("DeferredSweepWorker: drained {Count} pairs", claims.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeferredSweepWorker: pass failed; retrying after {PollDelay}s", pollDelay.TotalSeconds);
                try { await Task.Delay(pollDelay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
