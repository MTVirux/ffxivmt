using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IItemStore
{
    Task<Item?> GetAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, string>> GetAllNamesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default);

    Task UpsertAsync(ItemUpsert item, CancellationToken ct = default);

    Task UpdateMarketableAsync(int id, bool marketable, CancellationToken ct = default);

    Task UpdateCraftableAsync(int id, bool craftable, CancellationToken ct = default);
}
