namespace Ffmt.Core.Gilflux;

internal static class GilfluxCacheKeys
{
    /// <summary>
    /// Mirrors the PHP cache key shape <c>gilflux_ranking_{location}_{all|crafted_only}</c> so the
    /// per-world keys populated when serving a single-world request can be reused as fallbacks when a
    /// later datacenter/region request walks the same world.
    /// </summary>
    public static string For(string location, bool craftedOnly) =>
        $"ffmt:gilflux_ranking_{location}_{(craftedOnly ? "crafted_only" : "all")}";
}
