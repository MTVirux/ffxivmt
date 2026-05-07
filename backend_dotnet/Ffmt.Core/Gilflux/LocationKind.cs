namespace Ffmt.Core.Gilflux;

public enum LocationKind
{
    World,
    Datacenter,
    Region,
}

public sealed record LocationResolution(LocationKind Kind, string CanonicalName, int? WorldId);
