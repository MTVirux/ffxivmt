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

        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

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

    /// <summary>Garland writes ids and amounts as a JSON number in some documents and a string in
    /// others, so every read has to accept both.</summary>
    private static bool TryReadInt(JsonElement el, out int value)
    {
        value = 0;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }

    private static bool TryFirstInt(JsonElement listing, string arrayKey, string propName, out int value)
    {
        value = 0;
        if (!listing.TryGetProperty(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return false;
        return first.TryGetProperty(propName, out var el) && TryReadInt(el, out value);
    }

    private static bool TryGetId(JsonElement entry, out int id)
    {
        id = 0;
        if (entry.ValueKind != JsonValueKind.Object) return false;
        return entry.TryGetProperty("id", out var idProp) && TryReadInt(idProp, out id);
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
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

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
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

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
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

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

                if (!TryFirstInt(listing, "item", "id", out var itemId)) continue;
                if (!TryFirstInt(listing, "currency", "id", out var curId)) continue;
                if (!TryFirstInt(listing, "currency", "amount", out var amount)) continue;

                result.Add(new GarlandTradeCurrencyListing(itemId, curId, amount));
            }
        }
        logger.LogInformation("Garland tradeCurrency for {Id}: {Count} listings.", currencyItemId, result.Count);
        return result;
    }

    public async Task<GarlandInstanceDetail?> GetInstanceAsync(int id, CancellationToken ct = default)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

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
            if (TryReadInt(idEl, out var id)) ids.Add(id);
        }
        return ids;
    }
}
