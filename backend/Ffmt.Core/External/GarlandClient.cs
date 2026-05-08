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

    public async Task<GarlandItemDetail?> GetItemDetailAsync(int id, CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        var path = $"item/en/3/{id.ToString(CultureInfo.InvariantCulture)}.json";
        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var name = string.Empty;
        if (root.TryGetProperty("item", out var itemEl) && itemEl.ValueKind == JsonValueKind.Object &&
            itemEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
        {
            name = nameEl.GetString() ?? string.Empty;
        }

        var relatedIds = ExtractItemPartialIds(root);
        return new GarlandItemDetail(id, name, relatedIds);
    }

    public async Task<IReadOnlyList<GarlandInstanceSummary>> GetAllInstancesAsync(CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        await using var stream = await http.GetStreamAsync("browse/en/2/instance.json", ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("browse", out var browse) || browse.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<GarlandInstanceSummary>();
        }

        var result = new List<GarlandInstanceSummary>(browse.GetArrayLength());
        foreach (var entry in browse.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("i", out var idEl) || !idEl.TryGetInt32(out var iid)) continue;
            var iname = entry.TryGetProperty("n", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
            var itype = entry.TryGetProperty("t", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;
            var min = entry.TryGetProperty("min_lvl", out var minEl) && minEl.ValueKind == JsonValueKind.Number ? minEl.GetInt32() : (int?)null;
            var max = entry.TryGetProperty("max_lvl", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number ? maxEl.GetInt32() : (int?)null;
            result.Add(new GarlandInstanceSummary(iid, iname, itype, min, max));
        }
        return result;
    }

    public async Task<IReadOnlyList<GarlandTradeCurrencyListing>> GetItemTradeCurrencyAsync(int currencyItemId, CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        var path = $"item/en/3/{currencyItemId.ToString(CultureInfo.InvariantCulture)}.json";
        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return Array.Empty<GarlandTradeCurrencyListing>();
        if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return Array.Empty<GarlandTradeCurrencyListing>();
        if (!item.TryGetProperty("tradeCurrency", out var trade) || trade.ValueKind != JsonValueKind.Array) return Array.Empty<GarlandTradeCurrencyListing>();

        var result = new List<GarlandTradeCurrencyListing>();
        foreach (var shop in trade.EnumerateArray())
        {
            if (shop.ValueKind != JsonValueKind.Object) continue;
            if (!shop.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array) continue;

            foreach (var listing in listings.EnumerateArray())
            {
                if (listing.ValueKind != JsonValueKind.Object) continue;

                if (!TryFirstId(listing, "item", out var itemId)) continue;
                if (!TryFirstId(listing, "currency", out var curId)) continue;
                if (!TryFirstAmount(listing, "currency", out var amount)) continue;

                result.Add(new GarlandTradeCurrencyListing(itemId, curId, amount));
            }
        }
        logger.LogInformation("Garland tradeCurrency for {Id}: {Count} listings.", currencyItemId, result.Count);
        return result;
    }

    private static bool TryFirstId(JsonElement listing, string key, out int id)
    {
        id = 0;
        if (!listing.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return false;
        if (!first.TryGetProperty("id", out var idEl)) return false;
        return idEl.ValueKind switch
        {
            JsonValueKind.Number => idEl.TryGetInt32(out id),
            JsonValueKind.String => int.TryParse(idEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id),
            _ => false,
        };
    }

    private static bool TryFirstAmount(JsonElement listing, string key, out int amount)
    {
        amount = 0;
        if (!listing.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return false;
        if (!first.TryGetProperty("amount", out var amEl)) return false;
        return amEl.ValueKind switch
        {
            JsonValueKind.Number => amEl.TryGetInt32(out amount),
            JsonValueKind.String => int.TryParse(amEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount),
            _ => false,
        };
    }

    public async Task<GarlandInstanceDetail?> GetInstanceAsync(int id, CancellationToken ct = default)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.UniversalisApi });

        var path = $"instance/en/2/{id.ToString(CultureInfo.InvariantCulture)}.json";
        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var loot = ExtractItemPartialIds(root);
        return new GarlandInstanceDetail(id, loot);
    }

    private static IReadOnlyList<int> ExtractItemPartialIds(JsonElement root)
    {
        if (!root.TryGetProperty("partials", out var partials) || partials.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var ids = new List<int>(partials.GetArrayLength());
        foreach (var partial in partials.EnumerateArray())
        {
            if (partial.ValueKind != JsonValueKind.Object) continue;
            if (!partial.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            if (typeEl.GetString() != "item") continue;
            if (!partial.TryGetProperty("id", out var idEl)) continue;

            // Garland's `id` field is sometimes a JSON number, sometimes a string.
            if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var nid))
            {
                ids.Add(nid);
            }
            else if (idEl.ValueKind == JsonValueKind.String && int.TryParse(idEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sid))
            {
                ids.Add(sid);
            }
        }
        return ids;
    }
}
