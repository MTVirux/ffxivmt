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
        using var _ = LogChannelScope.Begin(log, LogChannels.ScyllaDb);

        var ids = await universalis.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);

        await ProgressLoop.RunAsync(
            ids, log, "Marked items as marketable",
            (id, token) => items.UpdateMarketableAsync(id, true, token),
            ProgressLoop.DefaultConcurrency, ct).ConfigureAwait(false);
    }
}
