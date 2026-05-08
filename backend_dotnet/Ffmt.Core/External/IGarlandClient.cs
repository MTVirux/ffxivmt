namespace Ffmt.Core.External;

/// <summary>
/// Garland Tools data lookups. Used by the <c>updatedb</c> CLI to flip <c>craftable</c>
/// on items with a recipe, and by the profit-calculator tools to walk recipe partials and
/// instance loot.
/// </summary>
public interface IGarlandClient
{
    /// <summary>
    /// Returns one entry per id in <paramref name="ids"/>; <c>HasCraft</c> is <c>true</c>
    /// when Garland reports any non-empty craft recipe for that item.
    /// </summary>
    Task<IReadOnlyList<GarlandItemFlags>> GetItemBatchAsync(IReadOnlyList<int> ids, CancellationToken ct = default);

    /// <summary>
    /// Single-item detail lookup. <c>RelatedItemIds</c> contains the ids from the response's
    /// <c>partials</c> array filtered to <c>type == "item"</c> — these are the recipe components
    /// or related items used by the <c>item_product_profit_calculator</c> tool.
    /// </summary>
    Task<GarlandItemDetail?> GetItemDetailAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Lists every instance Garland knows about (the <c>browse/instance.json</c> endpoint).
    /// </summary>
    Task<IReadOnlyList<GarlandInstanceSummary>> GetAllInstancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Single instance detail; <c>LootItemIds</c> comes from the <c>partials</c> array filtered
    /// to <c>type == "item"</c>.
    /// </summary>
    Task<GarlandInstanceDetail?> GetInstanceAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Walks <c>item.tradeCurrency[*].listings[*]</c> — one entry per (exchanged item, currency, amount).
    /// </summary>
    Task<IReadOnlyList<GarlandTradeCurrencyListing>> GetItemTradeCurrencyAsync(int currencyItemId, CancellationToken ct = default);
}

public sealed record GarlandItemFlags(int Id, bool HasCraft);

public sealed record GarlandItemDetail(int Id, string Name, IReadOnlyList<int> RelatedItemIds);

public sealed record GarlandInstanceSummary(int Id, string Name, string Type, int? MinLevel, int? MaxLevel);

public sealed record GarlandInstanceDetail(int Id, IReadOnlyList<int> LootItemIds);

public sealed record GarlandTradeCurrencyListing(int ItemId, int CurrencyId, int CurrencyAmount);
