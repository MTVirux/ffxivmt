using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Elastic;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateElasticStage(IElasticItemSearch elastic, ILogger<UpdateElasticStage> log)
{
    public async Task RunAsync(IReadOnlyList<ItemUpsert> rows, CancellationToken ct)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await elastic.UpsertAsync(rows[i].Id, rows[i].Name, ct).ConfigureAwait(false);
            if ((i + 1) % 1000 == 0)
            {
                log.LogInformation("Indexed {Done}/{Total} items into Elasticsearch.", i + 1, rows.Count);
            }
        }
        log.LogInformation("Indexed {Count} items into Elasticsearch.", rows.Count);
    }
}
