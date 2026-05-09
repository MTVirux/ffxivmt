using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

/// <summary>API-shaped row: a GilfluxRanking enriched with item/world fields the
/// underlying Scylla rows no longer carry.</summary>
public sealed record EnrichedGilfluxRanking(
    int ItemId,
    string ItemName,
    int? WorldId,
    string? WorldName,
    string Datacenter,
    string Region,
    long Ranking1h,
    long Ranking3h,
    long Ranking6h,
    long Ranking12h,
    long Ranking1d,
    long Ranking3d,
    long Ranking7d,
    long? UpdatedAt,
    long? LastSaleTime);

public sealed class GilfluxRankingReader
{
    private readonly IGilfluxRankingStore _store;
    private readonly IWorldStore _worldStore;
    private readonly IItemStore _itemStore;
    private readonly LocationResolver _resolver;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public GilfluxRankingReader(
        IGilfluxRankingStore store,
        IWorldStore worldStore,
        IItemStore itemStore,
        LocationResolver resolver,
        IMemoryCache cache,
        IOptions<GilfluxOptions> options)
    {
        _store = store;
        _worldStore = worldStore;
        _itemStore = itemStore;
        _resolver = resolver;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.RankingCacheSeconds));
    }

    public async Task<RankingByLocationResult?> GetByLocationAsync(string targetLocation, bool craftedOnly, CancellationToken ct = default)
    {
        var resolution = await _resolver.ResolveAsync(targetLocation, ct).ConfigureAwait(false);
        if (resolution is null)
        {
            return null;
        }

        var requestedKey = GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly);
        if (_cache.TryGetValue(requestedKey, out IReadOnlyList<EnrichedGilfluxRanking>? cached) && cached is not null)
        {
            return new RankingByLocationResult(resolution, cached, FromCache: true);
        }

        var allWorlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var worldsById = allWorlds.ToDictionary(w => w.Id);
        var itemNames = await _itemStore.GetAllNamesAsync(ct).ConfigureAwait(false);

        IReadOnlyList<EnrichedGilfluxRanking> enrichedAll = resolution.Kind switch
        {
            LocationKind.World      => Enrich(await _store.GetByWorldAsync(resolution.WorldId!.Value, ct).ConfigureAwait(false), worldsById, itemNames),
            LocationKind.Datacenter => Enrich(await MergeRawByDatacenterAsync(resolution.CanonicalName, ct).ConfigureAwait(false), worldsById, itemNames),
            LocationKind.Region     => Enrich(await MergeRawByRegionAsync(resolution.CanonicalName, ct).ConfigureAwait(false), worldsById, itemNames),
            _ => Array.Empty<EnrichedGilfluxRanking>(),
        };

        // Always populate the unfiltered cache so siblings that include this location can reuse it.
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: false), enrichedAll, _ttl);

        if (!craftedOnly)
        {
            return new RankingByLocationResult(resolution, enrichedAll, FromCache: false);
        }

        var craftableIds = (await _itemStore.GetCraftableIdsAsync(ct).ConfigureAwait(false)).ToHashSet();
        var crafted = enrichedAll.Where(r => craftableIds.Contains(r.ItemId)).ToList();
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: true), (IReadOnlyList<EnrichedGilfluxRanking>)crafted, _ttl);
        return new RankingByLocationResult(resolution, crafted, FromCache: false);
    }

    /// <summary>Public helper exposed for endpoints that want to enrich a list returned
    /// directly by the store (e.g. the per-item endpoint).</summary>
    public async Task<IReadOnlyList<EnrichedGilfluxRanking>> EnrichAsync(
        IEnumerable<GilfluxRanking> rows, CancellationToken ct = default)
    {
        var allWorlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var worldsById = allWorlds.ToDictionary(w => w.Id);
        var itemNames = await _itemStore.GetAllNamesAsync(ct).ConfigureAwait(false);
        return Enrich(rows, worldsById, itemNames);
    }

    private async Task<IReadOnlyList<GilfluxRanking>> MergeRawByDatacenterAsync(string datacenter, CancellationToken ct)
    {
        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var dcWorlds = worlds.Where(w => string.Equals(w.Datacenter, datacenter, StringComparison.OrdinalIgnoreCase)).ToList();
        var perWorldTasks = dcWorlds.Select(w => _store.GetByWorldAsync(w.Id, ct)).ToArray();
        await Task.WhenAll(perWorldTasks).ConfigureAwait(false);
        return perWorldTasks.SelectMany(t => t.Result).ToList();
    }

    private async Task<IReadOnlyList<GilfluxRanking>> MergeRawByRegionAsync(string region, CancellationToken ct)
    {
        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var regionWorlds = worlds.Where(w => string.Equals(w.Region, region, StringComparison.OrdinalIgnoreCase)).ToList();
        var perWorldTasks = regionWorlds.Select(w => _store.GetByWorldAsync(w.Id, ct)).ToArray();
        await Task.WhenAll(perWorldTasks).ConfigureAwait(false);
        return perWorldTasks.SelectMany(t => t.Result).ToList();
    }

    private static IReadOnlyList<EnrichedGilfluxRanking> Enrich(
        IEnumerable<GilfluxRanking> rows,
        IReadOnlyDictionary<int, World> worldsById,
        IReadOnlyDictionary<int, string> itemNames)
    {
        var result = new List<EnrichedGilfluxRanking>();
        foreach (var r in rows)
        {
            var (worldName, datacenter, region) = ResolveLocation(r.WorldId, worldsById);
            var itemName = itemNames.TryGetValue(r.ItemId, out var n) ? n : string.Empty;
            result.Add(new EnrichedGilfluxRanking(
                ItemId: r.ItemId,
                ItemName: itemName,
                WorldId: r.WorldId,
                WorldName: worldName,
                Datacenter: datacenter,
                Region: region,
                Ranking1h: r.Ranking1h,
                Ranking3h: r.Ranking3h,
                Ranking6h: r.Ranking6h,
                Ranking12h: r.Ranking12h,
                Ranking1d: r.Ranking1d,
                Ranking3d: r.Ranking3d,
                Ranking7d: r.Ranking7d,
                UpdatedAt: r.UpdatedAt,
                LastSaleTime: r.LastSaleTime));
        }
        return result;
    }

    private static (string? WorldName, string Datacenter, string Region) ResolveLocation(
        int? worldId, IReadOnlyDictionary<int, World> worldsById)
    {
        if (worldId is null) return (null, string.Empty, string.Empty);
        return worldsById.TryGetValue(worldId.Value, out var w)
            ? (w.Name, w.Datacenter, w.Region)
            : (null, string.Empty, string.Empty);
    }
}

public sealed record RankingByLocationResult(
    LocationResolution Resolution,
    IReadOnlyList<EnrichedGilfluxRanking> Rankings,
    bool FromCache);
