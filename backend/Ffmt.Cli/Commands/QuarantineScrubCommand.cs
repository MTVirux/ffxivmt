using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Cli.Commands;

public sealed class QuarantineScrubCommand(
    ISaleStore saleStore,
    IQuarantineStore quarantineStore,
    ISaleAnomalyFilter filter,
    IPriceBaselineProvider baselines,
    IWorldStore worldStore,
    WorldStructureService worldStructure,
    IDirtyPairQueue dirtyPairs,
    IOptions<GilfluxOptions> gilflux,
    IOptions<QuarantineOptions> quarantine,
    ILogger<QuarantineScrubCommand> logger)
{
    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        if (!quarantine.Value.Enabled)
        {
            logger.LogWarning("Quarantine is disabled - nothing to scrub.");
            return;
        }

        await baselines.EnsureLoadedAsync(ct).ConfigureAwait(false);

        var windowDays = (int)Math.Ceiling(
            TimeSpan.FromMilliseconds(gilflux.Value.TimeframesMs.Values.Max()).TotalDays) + 1;
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var itemIds = await worldStructure.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);

        logger.LogInformation("{Mode} scrub: {Worlds} world(s), {Items} item(s), {Days}d window",
            dryRun ? "DRY-RUN" : "Running", worlds.Count, itemIds.Count, windowDays);

        var totalQuarantined = 0;
        var affected = new HashSet<(int WorldId, int ItemId)>();

        foreach (var world in worlds)
        {
            var perWorld = 0;

            foreach (var itemId in itemIds)
            {
                ct.ThrowIfCancellationRequested();

                for (var i = 0; i <= windowDays; i++)
                {
                    var date = today.AddDays(-i);
                    var sales = await saleStore
                        .GetByItemAndWorldInRangeAsync(itemId, world.Id, date, ct).ConfigureAwait(false);
                    if (sales.Count == 0)
                    {
                        continue;
                    }

                    var partition = await filter.PartitionAsync(sales, ct).ConfigureAwait(false);

                    if (!dryRun)
                    {
                        await saleStore.BackfillTotalPriceAsync(partition.Accepted, ct).ConfigureAwait(false);
                    }

                    if (partition.Quarantined.Count == 0)
                    {
                        continue;
                    }

                    perWorld += partition.Quarantined.Count;
                    totalQuarantined += partition.Quarantined.Count;
                    affected.Add((world.Id, itemId));

                    if (!dryRun)
                    {
                        await quarantineStore.AddBatchAsync(partition.Quarantined, ct).ConfigureAwait(false);
                        await saleStore.DeleteExactAsync(
                            partition.Quarantined.Select(q => q.Sale).ToList(), ct).ConfigureAwait(false);
                    }
                }
            }

            if (perWorld > 0)
            {
                logger.LogInformation("{Mode} {World}: {Count} anomalous sale(s)",
                    dryRun ? "DRY-RUN" : "Quarantined", world.Name, perWorld);
            }
        }

        if (!dryRun && affected.Count > 0)
        {
            await dirtyPairs.EnqueueManyAsync(affected, ct).ConfigureAwait(false);
            logger.LogInformation("Enqueued {Count} pair(s) for a gilflux refresh", affected.Count);
        }

        logger.LogInformation("{Mode} scrub finished: {Total} anomalous sale(s) across {Pairs} pair(s)",
            dryRun ? "DRY-RUN" : "Scrub", totalQuarantined, affected.Count);
    }
}
