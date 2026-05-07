using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IItemStore
{
    Task<Item?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>id → name lookup over the entire <c>items</c> table; cached by callers (e.g. ingestion hot path).</summary>
    Task<IReadOnlyDictionary<int, string>> GetAllNamesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default);

    /// <summary>Inserts the full CSV-derived row (used by <c>updatedb</c> CLI's <c>update-items</c> stage).</summary>
    Task UpsertAsync(ItemUpsert item, CancellationToken ct = default);

    Task UpdateMarketableAsync(int id, bool marketable, CancellationToken ct = default);

    Task UpdateCraftableAsync(int id, bool craftable, CancellationToken ct = default);
}
