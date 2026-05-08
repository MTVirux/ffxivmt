using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Core.Gilflux;

/// <summary>
/// Maps a free-form <c>target_location</c> query parameter to its canonical world/datacenter/region.
/// Mirrors the case-insensitive lookup the PHP <c>api/v1/Gilflux::index_get</c> performs against the worlds table.
/// </summary>
public sealed class LocationResolver(IWorldStore worldStore)
{
    public async Task<LocationResolution?> ResolveAsync(string target, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        if (worlds.Count == 0)
        {
            return null;
        }

        var trimmed = target.Trim();

        foreach (var w in worlds)
        {
            if (string.Equals(w.Region, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return new LocationResolution(LocationKind.Region, w.Region, null);
            }
        }

        foreach (var w in worlds)
        {
            if (string.Equals(w.Datacenter, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return new LocationResolution(LocationKind.Datacenter, w.Datacenter, null);
            }
        }

        foreach (var w in worlds)
        {
            if (string.Equals(w.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return new LocationResolution(LocationKind.World, w.Name, w.Id);
            }
        }

        return null;
    }
}
