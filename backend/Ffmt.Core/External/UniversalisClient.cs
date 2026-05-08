using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> GetMarketBoardDataAsync(
        string location, IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("location is required", nameof(location));
        }
        if (itemIds.Count == 0)
        {
            return new Dictionary<int, UniversalisMarketBoardListing>();
        }

        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        // Universalis returns a multi-id shape (with an `items` dictionary) when the path has
        // a comma, and a flat single-item object otherwise.
        var idsPath = string.Join(",", itemIds.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        var path = $"{Uri.EscapeDataString(location)}/{idsPath}";

        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var result = new Dictionary<int, UniversalisMarketBoardListing>();
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            logger.LogWarning("Universalis returned a non-object response for {Location}/{Ids}.", location, idsPath);
            return result;
        }

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in items.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    continue;
                }
                result[id] = ParseListing(prop.Value);
            }
        }
        else if (root.TryGetProperty("itemID", out var idProp) && idProp.TryGetInt32(out var singleId))
        {
            result[singleId] = ParseListing(root);
        }

        logger.LogInformation("Universalis MB data: {Hits}/{Requested} items at {Location}.", result.Count, itemIds.Count, location);
        return result;
    }

    private static UniversalisMarketBoardListing ParseListing(JsonElement element)
    {
        var minPrice = element.TryGetProperty("minPrice", out var mp) && mp.ValueKind == JsonValueKind.Number
            ? mp.GetInt32()
            : 0;
        var velocity = element.TryGetProperty("regularSaleVelocity", out var rv) && rv.ValueKind == JsonValueKind.Number
            ? rv.GetDouble()
            : 0d;
        var histogram = ParseStackSizeHistogram(element);
        return new UniversalisMarketBoardListing(minPrice, velocity, histogram);
    }

    private static IReadOnlyDictionary<int, int> ParseStackSizeHistogram(JsonElement element)
    {
        if (!element.TryGetProperty("stackSizeHistogram", out var histo) || histo.ValueKind != JsonValueKind.Object)
        {
            return EmptyHistogram;
        }

        var result = new Dictionary<int, int>();
        foreach (var prop in histo.EnumerateObject())
        {
            if (!int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;
            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var occ)) continue;
            result[size] = occ;
        }
        return result;
    }

    private static readonly IReadOnlyDictionary<int, int> EmptyHistogram = new Dictionary<int, int>();

    private sealed record UniversalisWorld(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record UniversalisDataCenter(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("region")] string Region,
        [property: JsonPropertyName("worlds")] int[] Worlds);
}
