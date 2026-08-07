using System.Globalization;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Worlds;

public sealed class WorldStructureService
{
    private const string TreeCacheKey = "ffmt:worlds:structure";
    private const string WorldsCacheKey = "ffmt:worlds:all";
    private const string WorldsByIdCacheKey = "ffmt:worlds:byId";
    private const string ItemNamesCacheKey = "ffmt:items:namesById";
    private const string MarketableIdsCacheKey = "ffmt:items:marketableIds";

    private readonly IWorldStore _worldStore;
    private readonly IItemStore _itemStore;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public WorldStructureService(
        IWorldStore worldStore,
        IItemStore itemStore,
        IMemoryCache cache,
        IOptions<GilfluxOptions> gilflux)
    {
        _worldStore = worldStore;
        _itemStore = itemStore;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, gilflux.Value.WorldStructureCacheSeconds));
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>>
        GetAsync(CancellationToken ct = default) =>
        GetOrCreateAsync(TreeCacheKey, async () => Build(await GetWorldsAsync(ct).ConfigureAwait(false)));

    public Task<IReadOnlyList<World>> GetWorldsAsync(CancellationToken ct = default) =>
        GetOrCreateAsync(WorldsCacheKey, () => _worldStore.GetAllAsync(ct));

    public Task<IReadOnlyDictionary<int, World>> GetWorldsByIdAsync(CancellationToken ct = default) =>
        GetOrCreateAsync(WorldsByIdCacheKey, async () =>
            (IReadOnlyDictionary<int, World>)(await GetWorldsAsync(ct).ConfigureAwait(false)).ToDictionary(w => w.Id));

    public async Task<World?> GetWorldAsync(int id, CancellationToken ct = default)
    {
        var byId = await GetWorldsByIdAsync(ct).ConfigureAwait(false);
        return byId.TryGetValue(id, out var w) ? w : null;
    }

    public Task<IReadOnlyDictionary<int, string>> GetItemNamesAsync(CancellationToken ct = default) =>
        GetOrCreateAsync(ItemNamesCacheKey, () => _itemStore.GetAllNamesAsync(ct));

    public Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default) =>
        GetOrCreateAsync(MarketableIdsCacheKey, () => _itemStore.GetMarketableIdsAsync(ct));

    private async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> load)
    {
        var value = await _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _ttl;
            return load();
        }).ConfigureAwait(false);
        return value!;
    }

    /// <summary>Restricts the tree to the regions we ingest, so the API never advertises a world
    /// we hold no sales for.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
        FilterToRegions(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> tree,
            IEnumerable<string> regions)
    {
        var allowed = new HashSet<string>(regions, StringComparer.OrdinalIgnoreCase);
        return tree
            .Where(kv => allowed.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
        Build(IReadOnlyList<World> worlds)
    {
        var byRegion = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);

        foreach (var regionGroup in worlds.GroupBy(w => w.Region, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var byDc = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

            foreach (var dcGroup in regionGroup.GroupBy(w => w.Datacenter, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var byWorld = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var w in dcGroup.OrderBy(w => w.Id))
                {
                    byWorld[w.Id.ToString(CultureInfo.InvariantCulture)] = w.Name;
                }
                byDc[dcGroup.Key] = byWorld;
            }

            byRegion[regionGroup.Key] = byDc;
        }

        return byRegion;
    }
}
