using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

/// <summary>
/// Read-side service for <c>GET /api/v1/gilflux</c>. Encapsulates the legacy "fan out by child world,
/// reuse per-world cache as fallback, then optionally filter to crafted-only" behaviour.
/// </summary>
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
        if (_cache.TryGetValue(requestedKey, out IReadOnlyList<GilfluxRanking>? cached) && cached is not null)
        {
            return new RankingByLocationResult(resolution, cached, FromCache: true);
        }

        IReadOnlyList<GilfluxRanking> all = resolution.Kind switch
        {
            LocationKind.World      => await _store.GetByWorldAsync(resolution.WorldId!.Value, ct).ConfigureAwait(false),
            LocationKind.Datacenter => await MergeByDatacenterAsync(resolution.CanonicalName, ct).ConfigureAwait(false),
            LocationKind.Region     => await MergeByRegionAsync(resolution.CanonicalName, ct).ConfigureAwait(false),
            _ => [],
        };

        // Always populate the unfiltered cache so siblings that include this location can reuse it.
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: false), all, _ttl);

        if (!craftedOnly)
        {
            return new RankingByLocationResult(resolution, all, FromCache: false);
        }

        var craftableIds = (await _itemStore.GetCraftableIdsAsync(ct).ConfigureAwait(false)).ToHashSet();
        var crafted = all.Where(r => craftableIds.Contains(r.ItemId)).ToList();
        // Only the filtered result lands in the crafted_only key — fixes the legacy bug where
        // a craft=false request would poison this slot with unfiltered data.
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: true), (IReadOnlyList<GilfluxRanking>)crafted, _ttl);
        return new RankingByLocationResult(resolution, crafted, FromCache: false);
    }

    private async Task<IReadOnlyList<GilfluxRanking>> MergeByDatacenterAsync(string datacenter, CancellationToken ct)
    {
        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var merged = new List<GilfluxRanking>();
        foreach (var w in worlds.Where(w => string.Equals(w.Datacenter, datacenter, StringComparison.OrdinalIgnoreCase)))
        {
            merged.AddRange(await GetWorldFromCacheOrStoreAsync(w.Id, w.Name, ct).ConfigureAwait(false));
        }
        return merged;
    }

    private async Task<IReadOnlyList<GilfluxRanking>> MergeByRegionAsync(string region, CancellationToken ct)
    {
        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var merged = new List<GilfluxRanking>();

        var byDatacenter = worlds
            .Where(w => string.Equals(w.Region, region, StringComparison.OrdinalIgnoreCase))
            .GroupBy(w => w.Datacenter, StringComparer.OrdinalIgnoreCase);

        foreach (var dcGroup in byDatacenter)
        {
            // Reuse a cached datacenter aggregate if a previous request already built it.
            var dcKey = GilfluxCacheKeys.For(dcGroup.Key, craftedOnly: false);
            if (_cache.TryGetValue(dcKey, out IReadOnlyList<GilfluxRanking>? dcCached) && dcCached is not null)
            {
                merged.AddRange(dcCached);
                continue;
            }

            foreach (var w in dcGroup)
            {
                merged.AddRange(await GetWorldFromCacheOrStoreAsync(w.Id, w.Name, ct).ConfigureAwait(false));
            }
        }
        return merged;
    }

    private async Task<IReadOnlyList<GilfluxRanking>> GetWorldFromCacheOrStoreAsync(int worldId, string worldName, CancellationToken ct)
    {
        var key = GilfluxCacheKeys.For(worldName, craftedOnly: false);
        if (_cache.TryGetValue(key, out IReadOnlyList<GilfluxRanking>? cached) && cached is not null)
        {
            return cached;
        }

        var fresh = await _store.GetByWorldAsync(worldId, ct).ConfigureAwait(false);
        _cache.Set(key, fresh, _ttl);
        return fresh;
    }
}

public sealed record RankingByLocationResult(LocationResolution Resolution, IReadOnlyList<GilfluxRanking> Rankings, bool FromCache);
