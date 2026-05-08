using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public sealed class UpdateWorldsStage(
    IUniversalisClient universalis,
    IWorldStore worlds,
    ILogger<UpdateWorldsStage> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var _ = log.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaDb });

        var fetched = await universalis.GetAllWorldsAsync(ct).ConfigureAwait(false);
        var added = 0;
        foreach (var world in fetched)
        {
            ct.ThrowIfCancellationRequested();
            await worlds.UpsertAsync(world, ct).ConfigureAwait(false);
            added++;
        }
        log.LogInformation("Upserted {Count}/{Total} worlds.", added, fetched.Count);
    }
}
