namespace Ffmt.Core.External;

/// <summary>
/// Sizing rules for Universalis history requests. Requests are not slow - a 15-item request over a
/// 9-day window returning 2.9MB came back in 3.2s - so the constraint here is what the upstream API
/// tolerates, not response size. Exceeding it returns 504, which the retry policy then surfaces as
/// a client timeout.
/// </summary>
public sealed class BackfillTuning
{
    public int ItemsPerRequest { get; set; } = 50;
    public int BaseRequestTimeoutSeconds { get; set; } = 60;
    public int PerWindowHourTimeoutSeconds { get; set; } = 10;
    public int MaxRequestTimeoutSeconds { get; set; } = 300;
    public int RetryRounds { get; set; } = 2;
    public int RetryRoundDelaySeconds { get; set; } = 20;

    /// <summary>
    /// In-flight requests per pass. Both loops run a pass concurrently, so the load reaching
    /// Universalis is double this. Raising it to 16 produced sustained 504s.
    /// </summary>
    public int Concurrency { get; set; } = 4;

    public TimeSpan RequestTimeoutFor(long windowSeconds)
    {
        var windowHours = windowSeconds / TimeSpan.FromHours(1).TotalSeconds;
        var seconds = BaseRequestTimeoutSeconds + (PerWindowHourTimeoutSeconds * windowHours);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRequestTimeoutSeconds));
    }
}
