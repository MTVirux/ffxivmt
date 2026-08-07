using Ffmt.Core.Models;
using Ffmt.Core.Storage.Elastic;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateElasticStage(IElasticItemSearch elastic, ILogger<UpdateElasticStage> log)
{
    private const int BatchSize = 1000;

    public async Task RunAsync(IReadOnlyList<ItemUpsert> rows, CancellationToken ct)
    {
        var done = 0;
        foreach (var batch in rows.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();
            await elastic.UpsertManyAsync(batch.Select(r => (r.Id, r.Name)), ct).ConfigureAwait(false);

            done += batch.Length;
            if (done % BatchSize == 0)
            {
                log.LogInformation("Indexed items into Elasticsearch: {Done}/{Total}.", done, rows.Count);
            }
        }
        log.LogInformation("Indexed items into Elasticsearch: {Total} total.", rows.Count);
    }
}
