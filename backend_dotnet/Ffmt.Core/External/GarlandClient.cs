using System.Globalization;
using System.Text.Json;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.External;

public sealed class GarlandClient(HttpClient http, ILogger<GarlandClient> logger) : IGarlandClient
{
    public const string HttpClientName = "garland";

    public async Task<IReadOnlyList<GarlandItemFlags>> GetItemBatchAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<GarlandItemFlags>();
        }

        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        var path = "item/en/3/" + string.Join(",", ids.Select(i => i.ToString(CultureInfo.InvariantCulture))) + ".json";

        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Garland returned non-array response for {ids.Count} ids.");
        }

        var result = new List<GarlandItemFlags>(doc.RootElement.GetArrayLength());
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!TryGetId(entry, out var id))
            {
                continue;
            }
            result.Add(new GarlandItemFlags(id, HasCraftRecipe(entry)));
        }
        return result;
    }

    private static bool TryGetId(JsonElement entry, out int id)
    {
        id = 0;
        if (entry.ValueKind != JsonValueKind.Object) return false;
        if (!entry.TryGetProperty("id", out var idProp)) return false;
        return idProp.ValueKind switch
        {
            JsonValueKind.Number => idProp.TryGetInt32(out id),
            JsonValueKind.String => int.TryParse(idProp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id),
            _ => false,
        };
    }

    private static bool HasCraftRecipe(JsonElement entry)
    {
        if (!entry.TryGetProperty("obj", out var obj) || obj.ValueKind != JsonValueKind.Object) return false;
        if (!obj.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return false;
        if (!item.TryGetProperty("craft", out var craft)) return false;
        return craft.ValueKind switch
        {
            JsonValueKind.Array => craft.GetArrayLength() > 0,
            JsonValueKind.Object => true,
            _ => false,
        };
    }
}
