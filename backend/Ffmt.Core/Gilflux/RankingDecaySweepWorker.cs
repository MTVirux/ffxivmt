using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

// gilflux_rankings is only written when a sale lands, so a pair that stopped selling keeps its
// last-computed sums forever. Deletes rows whose widest timeframe has lapsed and re-queues
// merely-stale ones through the dirty-pair queue, so DeferredSweepWorker recomputes them under
// its own concurrency bound.
public sealed class RankingDecaySweepWorker : BackgroundService
{
    private readonly IWorldStore _worldStore;
    private readonly IGilfluxRankingStore _store;
    private readonly IDirtyPairQueue _queue;
    private readonly GilfluxOptions _options;
    private readonly ILogger<RankingDecaySweepWorker> _logger;

    public RankingDecaySweepWorker(
        IWorldStore worldStore,
        IGilfluxRankingStore store,
        IDirtyPairQueue queue,
        IOptions<GilfluxOptions> options,
        ILogger<RankingDecaySweepWorker> logger)
    {
        _worldStore = worldStore;
        _store = store;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.DecaySweepEnabled)
        {
            _logger.LogInformation("RankingDecaySweepWorker disabled by configuration");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.DecaySweepIntervalSeconds));
        var staleAfter = TimeSpan.FromSeconds(Math.Max(60, _options.DecaySweepStaleAfterSeconds));
        var maxRefresh = Math.Max(0, _options.DecaySweepMaxRefreshPerWorld);
        var maxDelete = Math.Max(0, _options.DecaySweepMaxDeletePerWorld);

        _logger.LogInformation(
            "RankingDecaySweepWorker started - interval={Interval}s staleAfter={StaleAfter}s refresh<={Refresh}/world delete<={Delete}/world",
            interval.TotalSeconds, staleAfter.TotalSeconds, maxRefresh, maxDelete);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(staleAfter, maxRefresh, maxDelete, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RankingDecaySweepWorker: pass failed; retrying after {Interval}s", interval.TotalSeconds);
            }

            try { await Task.Delay(interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(TimeSpan staleAfter, int maxRefresh, int maxDelete, CancellationToken ct)
    {
        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        if (worlds.Count == 0)
        {
            return;
        }

        var scanned = 0;
        var deleted = 0;
        var enqueued = 0;

        foreach (var world in worlds)
        {
            ct.ThrowIfCancellationRequested();

            var rows = await _store.GetByWorldAsync(world.Id, ct).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                continue;
            }

            scanned += rows.Count;

            var plan = RankingDecay.Plan(
                rows, _options.TimeframesMs, staleAfter, maxRefresh, DateTimeOffset.UtcNow, maxDelete);

            if (plan.Delete.Count > 0)
            {
                await _store.DeleteManyAsync(plan.Delete, ct).ConfigureAwait(false);
                deleted += plan.Delete.Count;
            }

            if (plan.Refresh.Count > 0)
            {
                await _queue.EnqueueManyAsync(plan.Refresh, ct).ConfigureAwait(false);
                enqueued += plan.Refresh.Count;
            }
        }

        MetricsCatalog.DecaySweepRowsScannedTotal.Inc(scanned);
        MetricsCatalog.DecaySweepRowsDeletedTotal.Inc(deleted);
        MetricsCatalog.DecaySweepRowsEnqueuedTotal.Inc(enqueued);

        _logger.LogInformation(
            "RankingDecaySweepWorker: scanned={Scanned} deleted={Deleted} enqueued={Enqueued}",
            scanned, deleted, enqueued);
    }
}
