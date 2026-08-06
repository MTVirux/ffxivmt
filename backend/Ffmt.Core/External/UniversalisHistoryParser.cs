using System.Text.Json;
using Ffmt.Core.Models;

namespace Ffmt.Core.External;

public static class UniversalisHistoryParser
{
    /// <summary>
    /// Universalis returns three shapes: a multi-item request yields "items" as an object keyed by
    /// item id, a single-item request yields "entries" at the root, and "items" as an array is
    /// accepted defensively. Handling only the array shape silently yields zero sales for every
    /// multi-item request, which is how a backfill can look healthy while importing nothing.
    /// </summary>
    public static List<Sale> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sales = new List<Sale>();

        if (root.TryGetProperty("items", out var items))
        {
            if (items.ValueKind == JsonValueKind.Object)
            {
                foreach (var itemProp in items.EnumerateObject())
                    ParseItemElement(itemProp.Value, sales);
            }
            else if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemEl in items.EnumerateArray())
                    ParseItemElement(itemEl, sales);
            }
        }
        else if (root.TryGetProperty("entries", out _))
        {
            ParseItemElement(root, sales);
        }

        return sales;
    }

    private static void ParseItemElement(JsonElement itemEl, List<Sale> sales)
    {
        if (!itemEl.TryGetProperty("itemID", out var itemIdEl) ||
            !itemEl.TryGetProperty("entries", out var entriesEl) ||
            entriesEl.ValueKind != JsonValueKind.Array)
            return;

        var itemId = itemIdEl.GetInt32();

        foreach (var entry in entriesEl.EnumerateArray())
        {
            var worldId = entry.TryGetProperty("worldID", out var wIdEl) ? wIdEl.GetInt32() : 0;
            var hq = entry.TryGetProperty("hq", out var hqEl) && hqEl.ValueKind == JsonValueKind.True;
            var onMannequin = entry.TryGetProperty("onMannequin", out var omEl) && omEl.ValueKind == JsonValueKind.True;
            var pricePerUnit = entry.TryGetProperty("pricePerUnit", out var ppuEl) ? ppuEl.GetInt32() : 0;
            var quantity = entry.TryGetProperty("quantity", out var qEl) ? qEl.GetInt32() : 0;
            var buyerName = entry.TryGetProperty("buyerName", out var bnEl) ? bnEl.GetString() ?? string.Empty : string.Empty;
            var saleTimeSeconds = entry.TryGetProperty("timestamp", out var tsEl) ? tsEl.GetInt64() : 0L;

            sales.Add(new Sale(
                ItemId:      itemId,
                WorldId:     worldId,
                BuyerName:   buyerName,
                Hq:          hq,
                OnMannequin: onMannequin,
                Quantity:    quantity,
                UnitPrice:   pricePerUnit,
                SaleTime:    DateTimeOffset.FromUnixTimeSeconds(saleTimeSeconds)));
        }
    }
}
