using System.Globalization;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Worlds;

/// <summary>
/// Builds the legacy region&gt;datacenter&gt;world tree consumed by <c>GET /api/v1/worlds</c>.
/// Cached for <see cref="GilfluxOptions.WorldStructureCacheSeconds"/> (default 300s, matching the PHP <c>cache_timers</c> entry).
/// </summary>
public sealed class WorldStructureService
{
    private const string CacheKey = "ffmt:worlds:structure";

    private readonly IWorldStore _worldStore;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public WorldStructureService(
        IWorldStore worldStore,
        IMemoryCache cache,
        IOptions<GilfluxOptions> gilflux)
    {
        _worldStore = worldStore;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, gilflux.Value.WorldStructureCacheSeconds));
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>>
        GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>? cached)
            && cached is not null)
        {
            return cached;
        }

        var worlds = await _worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var built = Build(worlds);
        _cache.Set(CacheKey, built, _ttl);
        return built;
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
