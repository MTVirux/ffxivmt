using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.External;

public sealed class UniversalisClient(HttpClient http, ILogger<UniversalisClient> logger) : IUniversalisClient
{
    public const string HttpClientName = "universalis";

    public async Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });
        var ids = await http.GetFromJsonAsync<int[]>("marketable", ct).ConfigureAwait(false);
        if (ids is null)
        {
            throw new InvalidOperationException("Universalis returned a null marketable id list.");
        }
        logger.LogInformation("Universalis returned {Count} marketable item ids.", ids.Length);
        return ids;
    }

    public async Task<IReadOnlyList<World>> GetAllWorldsAsync(CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        var worldsTask = http.GetFromJsonAsync<UniversalisWorld[]>("worlds", ct);
        var dcsTask = http.GetFromJsonAsync<UniversalisDataCenter[]>("data-centers", ct);
        await Task.WhenAll(worldsTask, dcsTask).ConfigureAwait(false);

        var worlds = await worldsTask.ConfigureAwait(false) ?? throw new InvalidOperationException("Universalis worlds endpoint returned null.");
        var dcs = await dcsTask.ConfigureAwait(false) ?? throw new InvalidOperationException("Universalis data-centers endpoint returned null.");

        var worldNamesById = worlds.ToDictionary(w => w.Id, w => w.Name);

        var result = new List<World>(worlds.Length);
        foreach (var dc in dcs)
        {
            foreach (var worldId in dc.Worlds)
            {
                if (!worldNamesById.TryGetValue(worldId, out var name))
                {
                    logger.LogWarning("Datacenter {Datacenter} references unknown world id {WorldId}.", dc.Name, worldId);
                    continue;
                }
                result.Add(new World(worldId, name, dc.Name, dc.Region));
            }
        }
        logger.LogInformation("Universalis topology: {WorldCount} worlds across {DcCount} datacenters.", result.Count, dcs.Length);
        return result;
    }

    private sealed record UniversalisWorld(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record UniversalisDataCenter(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("region")] string Region,
        [property: JsonPropertyName("worlds")] int[] Worlds);
}
