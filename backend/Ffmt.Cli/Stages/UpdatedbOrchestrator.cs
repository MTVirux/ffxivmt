using Ffmt.Cli.Items;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdatedbOrchestrator(
    UpdateWorldsStage updateWorlds,
    ItemCsvSource csv,
    UpdateItemsStage updateItems,
    UpdateElasticStage updateElastic,
    UpdateGarlandStage updateGarland,
    UpdateMarketabilityStage updateMarketability,
    FixGilfluxNamesStage fixGilfluxNames,
    ILogger<UpdatedbOrchestrator> log)
{
    public async Task RunAllAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.DbUpdateActivations });

        log.LogInformation("=== updatedb stage 1/7: update-worlds ===");
        await updateWorlds.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 2/7: parse item CSV ===");
        var rows = await csv.LoadAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 3/7: update-items ===");
        await updateItems.RunAsync(rows, ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 4/7: update-elastic ===");
        await updateElastic.RunAsync(rows, ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 5/7: update-garland ===");
        await updateGarland.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 6/7: update-marketability ===");
        await updateMarketability.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 7/7: fix-gilflux-names ===");
        await fixGilfluxNames.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb complete ===");
    }
}
