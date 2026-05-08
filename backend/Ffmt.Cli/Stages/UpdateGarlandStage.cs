using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Cli.Stages;

public sealed class UpdateGarlandStage(
    IGarlandClient garland,
    IItemStore items,
    IOptions<UpdatedbOptions> options,
    ILogger<UpdateGarlandStage> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaDb });

        var ids = (await items.GetAllIdsAsync(ct).ConfigureAwait(false)).OrderBy(x => x).ToList();
        var batchSize = Math.Max(1, options.Value.GarlandBatchSize);
        var craftableFlipped = 0;
        var processed = 0;

        for (var offset = 0; offset < ids.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var slice = ids.Skip(offset).Take(batchSize).ToArray();
            var responses = await garland.GetItemBatchAsync(slice, ct).ConfigureAwait(false);

            foreach (var entry in responses)
            {
                if (entry.HasCraft)
                {
                    await items.UpdateCraftableAsync(entry.Id, true, ct).ConfigureAwait(false);
                    craftableFlipped++;
                }
            }
            processed += slice.Length;
            log.LogInformation("Garland: {Done}/{Total} items processed; craftable so far: {Craftable}.", processed, ids.Count, craftableFlipped);
        }

        log.LogInformation("Garland flagged {Count} of {Total} items as craftable.", craftableFlipped, ids.Count);
    }
}
