using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class FixGilfluxNamesStage(
    IGilfluxRankingStore rankings,
    IWorldStore worlds,
    ILogger<FixGilfluxNamesStage> log)
{
    /// <summary>
    /// Walks <c>gilflux_ranking</c> looking for rows whose <c>item_name</c> is empty and
    /// recomputes the ranking row across every world (which re-populates the names).
    /// Runs until the missing-name scan returns no more rows.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaGilflux });

        var allWorlds = await worlds.GetAllAsync(ct).ConfigureAwait(false);
        if (allWorlds.Count == 0)
        {
            log.LogWarning("FixGilfluxNames: worlds table is empty; nothing to refresh.");
            return;
        }

        var fixedItems = 0;
        while (!ct.IsCancellationRequested)
        {
            var missing = await rankings.GetOneItemIdWithMissingNameAsync(ct).ConfigureAwait(false);
            if (missing is null)
            {
                break;
            }

            log.LogInformation("FixGilfluxNames: refreshing item {ItemId} across {WorldCount} worlds.", missing.Value, allWorlds.Count);
            foreach (var world in allWorlds)
            {
                ct.ThrowIfCancellationRequested();
                await rankings.UpdateRankingAsync(world.Id, missing.Value, ct).ConfigureAwait(false);
            }
            fixedItems++;
        }
        log.LogInformation("FixGilfluxNames complete; refreshed {Count} items.", fixedItems);
    }
}
