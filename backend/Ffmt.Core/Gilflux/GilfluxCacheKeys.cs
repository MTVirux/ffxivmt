namespace Ffmt.Core.Gilflux;

internal static class GilfluxCacheKeys
{
    /// <summary>Per-world keys are reused as fallbacks when a later DC/region request walks the same world.</summary>
    public static string For(string location, bool craftedOnly) =>
        $"ffmt:gilflux_ranking_{location}_{(craftedOnly ? "crafted_only" : "all")}";
}
