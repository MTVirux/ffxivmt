namespace Ffmt.Core.Gilflux;

internal static class GilfluxCacheKeys
{
    public static string For(string location, bool craftedOnly) =>
        $"ffmt:gilflux_ranking_{location}_{(craftedOnly ? "crafted_only" : "all")}";
}
