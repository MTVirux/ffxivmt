namespace Ffmt.Core.Configuration;

public sealed class GilfluxOptions
{
    public const string SectionName = "Gilflux";

    public int RankingCacheSeconds { get; init; } = 20;
    public int WorldStructureCacheSeconds { get; init; } = 300;

    public double CoalesceWindowSeconds { get; init; } = 2.0;
    public int CoalesceWorkers { get; init; } = 8;
    public int CoalesceQueueMax { get; init; } = 1000;

    public int DeferredSweepPollSeconds { get; init; } = 5;
    public int DeferredSweepClaimBatch { get; init; } = 100;
    public int DeferredSweepConcurrency { get; init; } = 4;
    public int DirtyPairBucket { get; init; } = 0;

    /// <summary>Added on top of the widest timeframe to derive the gilflux_rankings row TTL.</summary>
    public int RankingTtlGraceSeconds { get; init; } = 86_400;

    public bool DecaySweepEnabled { get; init; } = true;
    public int DecaySweepIntervalSeconds { get; init; } = 300;
    public int DecaySweepStaleAfterSeconds { get; init; } = 900;
    public int DecaySweepMaxRefreshPerWorld { get; init; } = 25;
    public int DecaySweepMaxDeletePerWorld { get; init; } = 500;

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
