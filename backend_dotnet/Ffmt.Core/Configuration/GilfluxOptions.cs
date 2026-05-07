namespace Ffmt.Core.Configuration;

public sealed class GilfluxOptions
{
    public const string SectionName = "Gilflux";

    public int RankingCacheSeconds { get; init; } = 20;
    public int WorldStructureCacheSeconds { get; init; } = 300;
    public int RankingUpdateMaxConcurrency { get; init; } = 0; // 0 = unlimited (Task.WhenAll fan-out)
    public int[] TimeframesMs { get; init; } =
    [
        3_600_000,       // 1h
        10_800_000,      // 3h
        21_600_000,      // 6h
        43_200_000,      // 12h
        86_400_000,      // 1d
        259_200_000,     // 3d
        604_800_000,     // 7d
    ];
}
