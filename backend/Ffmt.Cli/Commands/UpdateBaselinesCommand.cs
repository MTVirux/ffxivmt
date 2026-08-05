using System.Diagnostics;
using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Cli.Commands;

public sealed class UpdateBaselinesCommand(
    ISaleStore saleStore,
    IScyllaSession scylla,
    IWorldStore worldStore,
    WorldStructureService worldStructure,
    IOptions<UniversalisOptions> universalis,
    IOptions<QuarantineOptions> quarantine,
    ILogger<UpdateBaselinesCommand> logger)
{
    private const string CqlUpsertBaseline = """
        INSERT INTO item_price_baseline
            (region, item_id, hq, median_unit_price, sample_count, computed_at)
        VALUES (?, ?, ?, ?, ?, ?)
        USING TTL ?
        """;

    public static (long Median, int Count) Aggregate(IReadOnlyList<PricePoint> points, bool hq)
    {
        var prices = points.Where(p => p.Hq == hq).Select(p => p.UnitPrice).ToList();
        return prices.Count == 0 ? (0L, 0) : (PriceMedian.Compute(prices), prices.Count);
    }

    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var opts = quarantine.Value;
        var sw = Stopwatch.StartNew();

        var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, opts.BaselineWindowDays));
        var ttlSeconds = Math.Max(1, opts.BaselineTtlDays) * 86_400;

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var itemIds = await worldStructure.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);
        var stmt = await scylla.PrepareAsync(CqlUpsertBaseline, ct).ConfigureAwait(false);

        using var semaphore = new SemaphoreSlim(opts.BaselineComputeConcurrency, opts.BaselineComputeConcurrency);

        foreach (var region in universalis.Value.RegionsToUse)
        {
            var regionWorlds = worlds
                .Where(w => string.Equals(w.Region, region, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (regionWorlds.Count == 0)
            {
                logger.LogWarning("No worlds resolved for region {Region} - skipping", region);
                continue;
            }

            logger.LogInformation("{Mode} baselines for {Region}: {Worlds} world(s), {Items} item(s), window {Days}d",
                dryRun ? "DRY-RUN" : "Computing", region, regionWorlds.Count, itemIds.Count, opts.BaselineWindowDays);

            var written = 0;

            foreach (var itemId in itemIds)
            {
                ct.ThrowIfCancellationRequested();

                var points = new List<PricePoint>();
                var tasks = regionWorlds.Select(async world =>
                {
                    await semaphore.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var slice = await saleStore
                            .GetPricePointsSinceAsync(itemId, world.Id, since, ct).ConfigureAwait(false);
                        lock (points) { points.AddRange(slice); }
                    }
                    finally { semaphore.Release(); }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (points.Count == 0)
                {
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                foreach (var hq in new[] { false, true })
                {
                    var (median, count) = Aggregate(points, hq);
                    if (count == 0)
                    {
                        continue;
                    }

                    if (!dryRun)
                    {
                        await scylla.MeasuredExecuteAsync(
                            stmt.Bind(region, itemId, hq, median, count, now, ttlSeconds),
                            "baseline_upsert").ConfigureAwait(false);
                    }
                    written++;
                }
            }

            logger.LogInformation("{Mode} {Region}: {Written} baseline row(s)",
                dryRun ? "DRY-RUN" : "Wrote", region, written);
        }

        sw.Stop();
        MetricsCatalog.BaselineJobDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        logger.LogInformation("update-baselines finished in {Seconds:F1}s", sw.Elapsed.TotalSeconds);
    }
}
