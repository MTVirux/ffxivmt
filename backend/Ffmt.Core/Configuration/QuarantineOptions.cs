namespace Ffmt.Core.Configuration;

public sealed class QuarantineOptions
{
    public const string SectionName = "Quarantine";

    public bool Enabled { get; init; } = true;

    // True records anomalies but still writes them to sales; set false to actually quarantine.
    public bool ShadowMode { get; init; } = true;

    public double MedianMultiplier { get; init; } = 20.0;
    public int MinSampleCount { get; init; } = 10;
    public long MinAbsoluteUnitPrice { get; init; } = 100_000;

    // Median lookback. Must not exceed what the archive leaves in Scylla.
    public int BaselineWindowDays { get; init; } = 7;

    public int BaselineTtlDays { get; init; } = 30;
    public int BaselineRefreshMinutes { get; init; } = 60;
    public int BaselineComputeConcurrency { get; init; } = 16;
}
