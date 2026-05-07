using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateItemsStage(IItemStore items, ILogger<UpdateItemsStage> log)
{
    public async Task RunAsync(IReadOnlyList<ItemUpsert> rows, CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaDb });

        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await items.UpsertAsync(rows[i], ct).ConfigureAwait(false);
            if ((i + 1) % 1000 == 0)
            {
                log.LogInformation("Upserted {Done}/{Total} items into Scylla.", i + 1, rows.Count);
            }
        }
        log.LogInformation("Upserted {Count} items into Scylla.", rows.Count);
    }
}
