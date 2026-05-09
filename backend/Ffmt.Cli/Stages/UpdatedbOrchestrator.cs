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
    ILogger<UpdatedbOrchestrator> log)
{
    public async Task RunAllAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.DbUpdateActivations });

        log.LogInformation("=== updatedb stage 1/6: update-worlds ===");
        await updateWorlds.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 2/6: parse item CSV ===");
        var rows = await csv.LoadAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 3/6: update-items ===");
        await updateItems.RunAsync(rows, ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 4/6: update-elastic ===");
        await updateElastic.RunAsync(rows, ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 5/6: update-garland ===");
        await updateGarland.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb stage 6/6: update-marketability ===");
        await updateMarketability.RunAsync(ct).ConfigureAwait(false);

        log.LogInformation("=== updatedb complete ===");
    }
}
