namespace Ffmt.Core.External;

/// <summary>
/// Sizing rules for Universalis history requests. The API only accepts <c>entriesWithin</c>
/// relative to now, so a crawl walking backwards asks for an ever-wider window and gets an
/// ever-larger response. Both the request timeout and the item batch size are therefore derived
/// from the window rather than fixed.
/// </summary>
public sealed class BackfillTuning
{
    public int ItemsPerRequest { get; set; } = 50;
    public int LargeWindowItemsPerRequest { get; set; } = 15;
    public int LargeWindowThresholdHours { get; set; } = 1;
    public int BaseRequestTimeoutSeconds { get; set; } = 60;
    public int PerWindowHourTimeoutSeconds { get; set; } = 10;
    public int MaxRequestTimeoutSeconds { get; set; } = 300;
    public int RetryRounds { get; set; } = 2;
    public int RetryRoundDelaySeconds { get; set; } = 20;

    /// <summary>
    /// In-flight requests per pass. The smaller batch triples the request count, so this has to
    /// cover a full pass inside the crawl interval; the shared token bucket still caps the rate.
    /// </summary>
    public int Concurrency { get; set; } = 16;

    public int ItemsPerRequestFor(long windowSeconds) =>
        windowSeconds > (long)TimeSpan.FromHours(LargeWindowThresholdHours).TotalSeconds
            ? LargeWindowItemsPerRequest
            : ItemsPerRequest;

    public TimeSpan RequestTimeoutFor(long windowSeconds)
    {
        var windowHours = windowSeconds / TimeSpan.FromHours(1).TotalSeconds;
        var seconds = BaseRequestTimeoutSeconds + (PerWindowHourTimeoutSeconds * windowHours);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRequestTimeoutSeconds));
    }
}
