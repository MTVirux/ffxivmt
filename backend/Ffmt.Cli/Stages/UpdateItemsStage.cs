using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateItemsStage(IItemStore items, ILogger<UpdateItemsStage> log)
{
    public async Task RunAsync(IReadOnlyList<ItemUpsert> rows, CancellationToken ct)
    {
        using var _ = LogChannelScope.Begin(log, LogChannels.ScyllaDb);

        await ProgressLoop.RunAsync(
            rows, log, "Upserted items into Scylla",
            (row, token) => items.UpsertAsync(row, token),
            ProgressLoop.DefaultConcurrency, ct).ConfigureAwait(false);
    }
}
