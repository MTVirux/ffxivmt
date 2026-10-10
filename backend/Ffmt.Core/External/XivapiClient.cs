using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.External;

public sealed class XivapiClient(HttpClient http, ILogger<XivapiClient> logger) : IXivapiClient
{
    public const string HttpClientName = "xivapi";

    private const int PageSize = 500;
    private const string CandidateQuery = "+ItemResult.Rarity>=2 +ItemResult.PriceLow>0 +ItemResult.EquipSlotCategory>0";
    private const string CandidateFields =
        "ItemResult.Rarity,ItemResult.LevelItem@as(raw),ItemResult.EquipSlotCategory@as(raw),ItemResult.ItemUICategory@as(raw)";
    private const string RecipeFields = "ItemResult@as(raw),AmountResult,Ingredient@as(raw),AmountIngredient";

    public async Task<IReadOnlyList<XivapiEquipmentCandidate>> GetEquipmentCandidatesAsync(CancellationToken ct = default)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

        var result = new List<XivapiEquipmentCandidate>();
        var path = $"search?sheets=Recipe&query={Escape(CandidateQuery)}&fields={Escape(CandidateFields)}&limit={PageSize}";
        while (true)
        {
            using var doc = await GetJsonAsync(path, ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var entry in results.EnumerateArray())
            {
                if (TryReadCandidate(entry, out var candidate))
                {
                    result.Add(candidate);
                }
            }

            if (!root.TryGetProperty("next", out var next) || next.ValueKind != JsonValueKind.String)
            {
                break;
            }

            // Later pages come back without the requested fields unless they are sent again.
            path = $"search?cursor={Escape(next.GetString()!)}&fields={Escape(CandidateFields)}&limit={PageSize}";
        }

        logger.LogInformation("XIVAPI: {Count} equipment recipe candidates.", result.Count);
        return result;
    }

    public async Task<IReadOnlyList<XivapiRecipe>> GetAllRecipesAsync(CancellationToken ct = default)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

        var recipes = await ReadSheetAsync("Recipe", RecipeFields, (_, fields) => ReadRecipe(fields), ct).ConfigureAwait(false);
        logger.LogInformation("XIVAPI: {Count} recipes.", recipes.Count);
        return recipes;
    }

    public async Task<IReadOnlyDictionary<int, int>> GetExpertDeliverySealsAsync(CancellationToken ct = default)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

        var rows = await ReadSheetAsync(
            "GCSupplyDutyReward",
            "SealsExpertDelivery",
            (rowId, fields) => new SealRow(rowId, ReadInt(fields, "SealsExpertDelivery")),
            ct).ConfigureAwait(false);
        return rows.Where(r => r.Seals > 0).ToDictionary(r => r.ItemLevel, r => r.Seals);
    }

    private async Task<List<T>> ReadSheetAsync<T>(string sheet, string fields, Func<int, JsonElement, T?> map, CancellationToken ct)
        where T : class
    {
        var result = new List<T>();
        int? after = null;
        while (true)
        {
            var path = $"sheet/{sheet}?fields={Escape(fields)}&limit={PageSize}"
                + (after is null ? string.Empty : $"&after={after.Value.ToString(CultureInfo.InvariantCulture)}");
            using var doc = await GetJsonAsync(path, ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            {
                return result;
            }

            var previous = after;
            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("row_id", out var idEl) || !idEl.TryGetInt32(out var rowId))
                {
                    continue;
                }

                after = rowId;
                if (row.TryGetProperty("fields", out var rowFields) && map(rowId, rowFields) is { } mapped)
                {
                    result.Add(mapped);
                }
            }

            if (after == previous)
            {
                return result;
            }
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        await using var stream = await http.GetStreamAsync(path, ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    private static bool TryReadCandidate(JsonElement entry, [NotNullWhen(true)] out XivapiEquipmentCandidate? candidate)
    {
        candidate = null;
        if (!entry.TryGetProperty("fields", out var fields) ||
            !fields.TryGetProperty("ItemResult", out var item) ||
            !item.TryGetProperty("row_id", out var idEl) || !idEl.TryGetInt32(out var id) ||
            !item.TryGetProperty("fields", out var itemFields))
        {
            return false;
        }

        candidate = new XivapiEquipmentCandidate(
            id,
            ReadInt(itemFields, "Rarity"),
            ReadInt(itemFields, "LevelItem@as(raw)"),
            ReadInt(itemFields, "EquipSlotCategory@as(raw)"),
            ReadInt(itemFields, "ItemUICategory@as(raw)"));
        return true;
    }

    private static XivapiRecipe? ReadRecipe(JsonElement fields)
    {
        var resultId = ReadInt(fields, "ItemResult@as(raw)");
        if (resultId <= 0 ||
            !fields.TryGetProperty("Ingredient@as(raw)", out var ids) || ids.ValueKind != JsonValueKind.Array ||
            !fields.TryGetProperty("AmountIngredient", out var amounts) || amounts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var idList = ids.EnumerateArray().ToList();
        var amountList = amounts.EnumerateArray().ToList();
        var ingredients = new List<XivapiIngredient>();
        for (var i = 0; i < Math.Min(idList.Count, amountList.Count); i++)
        {
            // Empty slots are 0 or -1.
            if (idList[i].TryGetInt32(out var id) && id > 0 && amountList[i].TryGetInt32(out var amount) && amount > 0)
            {
                ingredients.Add(new XivapiIngredient(id, amount));
            }
        }

        return ingredients.Count == 0
            ? null
            : new XivapiRecipe(resultId, Math.Max(1, ReadInt(fields, "AmountResult")), ingredients);
    }

    private static int ReadInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value) ? value : 0;

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record SealRow(int ItemLevel, int Seals);
}
