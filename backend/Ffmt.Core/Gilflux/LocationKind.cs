using Ffmt.Core.Models;

namespace Ffmt.Core.Gilflux;

public enum LocationKind
{
    World,
    Datacenter,
    Region,
}

public sealed record LocationResolution(LocationKind Kind, string CanonicalName, int? WorldId)
{
    /// <summary>The single "is this world in scope" predicate every location-scoped read shares.</summary>
    public bool Matches(World world) => Kind switch
    {
        LocationKind.World => WorldId is not null && world.Id == WorldId.Value,
        LocationKind.Datacenter => string.Equals(world.Datacenter, CanonicalName, StringComparison.OrdinalIgnoreCase),
        LocationKind.Region => string.Equals(world.Region, CanonicalName, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
