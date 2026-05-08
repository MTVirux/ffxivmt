using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateMarketabilityStage(
    IUniversalisClient universalis,
    IItemStore items,
    ILogger<UpdateMarketabilityStage> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaDb });

        var ids = await universalis.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);
        var done = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            await items.UpdateMarketableAsync(id, true, ct).ConfigureAwait(false);
            done++;
            if (done % 1000 == 0)
            {
                log.LogInformation("Marked {Done}/{Total} items as marketable.", done, ids.Count);
            }
        }
        log.LogInformation("Marked {Count} items as marketable.", done);
    }
}
