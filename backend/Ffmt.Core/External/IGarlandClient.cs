namespace Ffmt.Core.External;

public interface IGarlandClient
{
    /// <summary>HasCraft is true when Garland reports a non-empty craft recipe.</summary>
    Task<IReadOnlyList<GarlandItemFlags>> GetItemBatchAsync(IReadOnlyList<int> ids, CancellationToken ct = default);

    /// <summary>RelatedItemIds is the response's <c>partials</c> array filtered to <c>type == "item"</c>.</summary>
    Task<GarlandItemDetail?> GetItemDetailAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<GarlandInstanceSummary>> GetAllInstancesAsync(CancellationToken ct = default);

    /// <summary>LootItemIds is <c>partials</c> filtered to <c>type == "item"</c>.</summary>
    Task<GarlandInstanceDetail?> GetInstanceAsync(int id, CancellationToken ct = default);

    /// <summary>One entry per (exchanged item, currency, amount) under <c>item.tradeCurrency[*].listings[*]</c>.</summary>
    Task<IReadOnlyList<GarlandTradeCurrencyListing>> GetItemTradeCurrencyAsync(int currencyItemId, CancellationToken ct = default);
}

public sealed record GarlandItemFlags(int Id, bool HasCraft);

public sealed record GarlandItemDetail(int Id, string Name, IReadOnlyList<int> RelatedItemIds);

public sealed record GarlandInstanceSummary(int Id, string Name, string Type, int? MinLevel, int? MaxLevel);

public sealed record GarlandInstanceDetail(int Id, IReadOnlyList<int> LootItemIds);

public sealed record GarlandTradeCurrencyListing(int ItemId, int CurrencyId, int CurrencyAmount);
