namespace Ffmt.Core.Configuration;

public sealed class GilfluxOptions
{
    public const string SectionName = "Gilflux";

    public int RankingCacheSeconds { get; init; } = 20;
    public int WorldStructureCacheSeconds { get; init; } = 300;
    public int RankingUpdateMaxConcurrency { get; init; } = 0; // 0 = unlimited (Task.WhenAll fan-out)

    public Dictionary<string, long> TimeframesMs { get; init; } = new()
    {
        ["7d"]  = 604_800_000,
        ["3d"]  = 259_200_000,
        ["1d"]  = 86_400_000,
        ["12h"] = 43_200_000,
        ["6h"]  = 21_600_000,
        ["3h"]  = 10_800_000,
        ["1h"]  = 3_600_000,
    };
}
