using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
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
        var prices = new List<int>();
        foreach (var p in points)
        {
            if (p.Hq == hq) prices.Add(p.UnitPrice);
        }
        return Summarize(prices);
    }

    private static (long Median, int Count) Summarize(List<int> prices) =>
        prices.Count == 0 ? (0L, 0) : (PriceMedian.Compute(prices), prices.Count);

    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var opts = quarantine.Value;
        var sw = Stopwatch.StartNew();

        if (universalis.Value.RegionsToUse.Length == 0)
        {
            logger.LogError("No regions configured under Universalis:RegionsToUse - no baselines to compute");
            return;
        }

        var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, opts.BaselineWindowDays));
        var ttlSeconds = Math.Max(1, opts.BaselineTtlDays) * 86_400;

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var itemIds = await worldStructure.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);
        var stmt = await scylla.PrepareAsync(CqlUpsertBaseline, ct).ConfigureAwait(false);

        using var semaphore = new SemaphoreSlim(opts.BaselineComputeConcurrency, opts.BaselineComputeConcurrency);

        foreach (var region in universalis.Value.RegionsToUse)
        {
            var regionScope = new LocationResolution(LocationKind.Region, region, null);
            var regionWorlds = worlds.Where(regionScope.Matches).ToList();

            if (regionWorlds.Count == 0)
            {
                logger.LogWarning("No worlds resolved for region {Region} - skipping", region);
                continue;
            }

            logger.LogInformation("{Mode} baselines for {Region}: {Worlds} world(s), {Items} item(s), window {Days}d",
                dryRun ? "DRY-RUN" : "Computing", region, regionWorlds.Count, itemIds.Count, opts.BaselineWindowDays);

            var written = 0;

            // One item spans only ~4-8 worlds, so per-item fan-out leaves most permits idle.
            // Batching widens the cross product without buffering every item's points at once.
            foreach (var itemBatch in itemIds.Chunk(Math.Max(1, opts.BaselineComputeConcurrency)))
            {
                ct.ThrowIfCancellationRequested();

                var pointsByItem = itemBatch.ToDictionary(id => id, _ => new List<PricePoint>());

                var fetches = itemBatch.SelectMany(itemId => regionWorlds.Select(async world =>
                {
                    await semaphore.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var slice = await saleStore
                            .GetPricePointsSinceAsync(itemId, world.Id, since, ct).ConfigureAwait(false);
                        var points = pointsByItem[itemId];
                        lock (points) { points.AddRange(slice); }
                    }
                    finally { semaphore.Release(); }
                }));

                await Task.WhenAll(fetches).ConfigureAwait(false);

                foreach (var itemId in itemBatch)
                {
                    written += await WriteBaselinesAsync(
                        stmt, region, itemId, pointsByItem[itemId], ttlSeconds, dryRun).ConfigureAwait(false);
                }
            }

            logger.LogInformation("{Mode} {Region}: {Written} baseline row(s)",
                dryRun ? "DRY-RUN" : "Wrote", region, written);
        }

        sw.Stop();
        MetricsCatalog.BaselineJobDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        logger.LogInformation("update-baselines finished in {Seconds:F1}s", sw.Elapsed.TotalSeconds);
    }

    /// <summary>Splits hq from nq in one pass; two <see cref="Aggregate"/> calls walk the list twice.</summary>
    private async Task<int> WriteBaselinesAsync(
        PreparedStatement stmt, string region, int itemId,
        List<PricePoint> points, int ttlSeconds, bool dryRun)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        var nqPrices = new List<int>(points.Count);
        var hqPrices = new List<int>();
        foreach (var p in points)
        {
            (p.Hq ? hqPrices : nqPrices).Add(p.UnitPrice);
        }

        var now = DateTimeOffset.UtcNow;
        var written = 0;

        foreach (var (hq, prices) in new[] { (false, nqPrices), (true, hqPrices) })
        {
            var (median, count) = Summarize(prices);
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
        return written;
    }
}
