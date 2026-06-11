using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ItemSalesReader(
    ISaleStore sales,
    IWorldStore worldStore,
    LocationResolver resolver)
{
    public async Task<IReadOnlyList<Sale>?> GetByItemAndLocationAsync(
        int itemId, string targetLocation, int limit, CancellationToken ct = default)
    {
        var resolution = await resolver.ResolveAsync(targetLocation, ct).ConfigureAwait(false);
        if (resolution is null)
        {
            return null;
        }

        if (resolution.Kind == LocationKind.World)
        {
            return await sales.GetByItemAndWorldAsync(itemId, resolution.WorldId!.Value, limit, ct).ConfigureAwait(false);
        }

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var scopeWorlds = worlds.Where(w => resolution.Kind == LocationKind.Datacenter
                ? string.Equals(w.Datacenter, resolution.CanonicalName, StringComparison.OrdinalIgnoreCase)
                : string.Equals(w.Region, resolution.CanonicalName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var perWorldTasks = scopeWorlds
            .Select(w => sales.GetByItemAndWorldAsync(itemId, w.Id, limit, ct))
            .ToArray();
        var perWorld = await Task.WhenAll(perWorldTasks).ConfigureAwait(false);

        return perWorld
            .SelectMany(r => r)
            .OrderByDescending(s => s.SaleTime)
            .Take(limit)
            .ToList();
    }
}
