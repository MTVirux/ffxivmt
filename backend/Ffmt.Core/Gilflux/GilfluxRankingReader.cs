using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed record EnrichedGilfluxRanking(
    int ItemId,
    string ItemName,
    int? WorldId,
    string? WorldName,
    string Datacenter,
    string Region,
    IReadOnlyDictionary<string, long> Rankings,
    long? UpdatedAt,
    long? LastSaleTime);

public sealed class GilfluxRankingReader
{
    private readonly IGilfluxRankingStore _store;
    private readonly WorldStructureService _worldStructure;
    private readonly IItemStore _itemStore;
    private readonly LocationResolver _resolver;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GilfluxRankingReader> _log;
    private readonly TimeSpan _ttl;
    private readonly IReadOnlyDictionary<string, long> _timeframesMs;

    public GilfluxRankingReader(
        IGilfluxRankingStore store,
        WorldStructureService worldStructure,
        IItemStore itemStore,
        LocationResolver resolver,
        IMemoryCache cache,
        IOptions<GilfluxOptions> options,
        ILogger<GilfluxRankingReader> log)
    {
        _store = store;
        _worldStructure = worldStructure;
        _itemStore = itemStore;
        _resolver = resolver;
        _cache = cache;
        _log = log;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.RankingCacheSeconds));
        _timeframesMs = options.Value.TimeframesMs;
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

        var worldsById = await _worldStructure.GetWorldsByIdAsync(ct).ConfigureAwait(false);
        var itemNames = await _worldStructure.GetItemNamesAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        var raw = resolution.Kind == LocationKind.World
            ? await _store.GetByWorldAsync(resolution.WorldId!.Value, ct).ConfigureAwait(false)
            : await MergeRawAsync(resolution, ct).ConfigureAwait(false);
        var enrichedAll = Enrich(raw, worldsById, itemNames, _timeframesMs, now);

        // Populate the unfiltered cache even on a crafted-only request - the crafted list is a
        // subset, so the full set is the reusable one.
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: false), enrichedAll, _ttl);

        if (!craftedOnly)
        {
            return new RankingByLocationResult(resolution, enrichedAll, FromCache: false);
        }

        var craftableIds = (await _itemStore.GetCraftableIdsAsync(ct).ConfigureAwait(false)).ToHashSet();
        if (craftableIds.Count == 0)
        {
            _log.LogWarning(
                "Crafted-only rankings requested but no item is flagged craftable - the filter will return nothing. Run 'ffmt update-garland'.");
        }

        var crafted = enrichedAll.Where(r => craftableIds.Contains(r.ItemId)).ToList();
        _cache.Set(GilfluxCacheKeys.For(resolution.CanonicalName, craftedOnly: true), (IReadOnlyList<EnrichedGilfluxRanking>)crafted, _ttl);
        return new RankingByLocationResult(resolution, crafted, FromCache: false);
    }

    public async Task<IReadOnlyList<EnrichedGilfluxRanking>?> GetByItemAndLocationAsync(
        int itemId, string targetLocation, CancellationToken ct = default)
    {
        var resolution = await _resolver.ResolveAsync(targetLocation, ct).ConfigureAwait(false);
        if (resolution is null)
        {
            return null;
        }

        var worldsById = await _worldStructure.GetWorldsByIdAsync(ct).ConfigureAwait(false);
        var itemNames = await _worldStructure.GetItemNamesAsync(ct).ConfigureAwait(false);

        IEnumerable<GilfluxRanking> raw;
        if (resolution.Kind == LocationKind.World)
        {
            raw = await _store.GetByItemAndWorldAsync(itemId, resolution.WorldId!.Value, ct).ConfigureAwait(false);
        }
        else
        {
            raw = (await _store.GetByItemAsync(itemId, ct).ConfigureAwait(false))
                .Where(r => r.WorldId is not null
                    && worldsById.TryGetValue(r.WorldId.Value, out var w)
                    && resolution.Matches(w));
        }

        return Enrich(raw, worldsById, itemNames, _timeframesMs, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<EnrichedGilfluxRanking>> EnrichAsync(
        IEnumerable<GilfluxRanking> rows, CancellationToken ct = default)
    {
        var worldsById = await _worldStructure.GetWorldsByIdAsync(ct).ConfigureAwait(false);
        var itemNames = await _worldStructure.GetItemNamesAsync(ct).ConfigureAwait(false);
        return Enrich(rows, worldsById, itemNames, _timeframesMs, DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<GilfluxRanking>> MergeRawAsync(
        LocationResolution resolution, CancellationToken ct)
    {
        var worlds = await _worldStructure.GetWorldsAsync(ct).ConfigureAwait(false);
        var perWorldTasks = worlds.Where(resolution.Matches).Select(w => _store.GetByWorldAsync(w.Id, ct)).ToArray();
        await Task.WhenAll(perWorldTasks).ConfigureAwait(false);
        return perWorldTasks.SelectMany(t => t.Result).ToList();
    }

    // Rows are only rewritten when a sale lands, so stored sums must be decayed before they are
    // served; a row with nothing left is not a mover and is dropped. Skipping the decay resurrects
    // stale top movers with frozen sums.
    private static IReadOnlyList<EnrichedGilfluxRanking> Enrich(
        IEnumerable<GilfluxRanking> rows,
        IReadOnlyDictionary<int, World> worldsById,
        IReadOnlyDictionary<int, string> itemNames,
        IReadOnlyDictionary<string, long> timeframesMs,
        DateTimeOffset now)
    {
        var result = new List<EnrichedGilfluxRanking>();
        foreach (var r in rows)
        {
            var rankings = RankingDecay.Apply(r.Rankings, timeframesMs, r.UpdatedAt, r.LastSaleTime, now);
            if (RankingDecay.IsExhausted(rankings))
            {
                continue;
            }

            var (worldName, datacenter, region) = ResolveLocation(r.WorldId, worldsById);
            var itemName = itemNames.TryGetValue(r.ItemId, out var n) ? n : string.Empty;
            result.Add(new EnrichedGilfluxRanking(
                ItemId: r.ItemId,
                ItemName: itemName,
                WorldId: r.WorldId,
                WorldName: worldName,
                Datacenter: datacenter,
                Region: region,
                Rankings: rankings,
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
