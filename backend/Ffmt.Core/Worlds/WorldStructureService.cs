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

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>>
        GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(TreeCacheKey, out IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>? cached)
            && cached is not null)
        {
            return cached;
        }

        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var built = Build(worlds);
        _cache.Set(TreeCacheKey, built, _ttl);
        return built;
    }

    public async Task<IReadOnlyDictionary<int, World>> GetWorldsByIdAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(WorldsByIdCacheKey, out IReadOnlyDictionary<int, World>? cached) && cached is not null)
        {
            return cached;
        }

        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var byId = (IReadOnlyDictionary<int, World>)worlds.ToDictionary(w => w.Id);
        _cache.Set(WorldsByIdCacheKey, byId, _ttl);
        return byId;
    }

    public async Task<World?> GetWorldAsync(int id, CancellationToken ct = default)
    {
        var byId = await GetWorldsByIdAsync(ct).ConfigureAwait(false);
        return byId.TryGetValue(id, out var w) ? w : null;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetItemNamesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(ItemNamesCacheKey, out IReadOnlyDictionary<int, string>? cached) && cached is not null)
        {
            return cached;
        }

        var names = await _itemStore.GetAllNamesAsync(ct).ConfigureAwait(false);
        _cache.Set(ItemNamesCacheKey, names, _ttl);
        return names;
    }

    public async Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(MarketableIdsCacheKey, out IReadOnlyList<int>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = await _itemStore.GetMarketableIdsAsync(ct).ConfigureAwait(false);
        _cache.Set(MarketableIdsCacheKey, ids, _ttl);
        return ids;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
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
