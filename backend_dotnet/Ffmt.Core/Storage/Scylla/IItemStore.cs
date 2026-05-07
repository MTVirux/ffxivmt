using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IItemStore
{
    Task<Item?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>id → name lookup over the entire <c>items</c> table; cached by callers (e.g. ingestion hot path).</summary>
    Task<IReadOnlyDictionary<int, string>> GetAllNamesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default);
}
