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
    public int LargeWindowThresholdDays { get; set; } = 1;
    public int BaseRequestTimeoutSeconds { get; set; } = 60;
    public int PerWindowDayTimeoutSeconds { get; set; } = 15;
    public int MaxRequestTimeoutSeconds { get; set; } = 300;
    public int RetryRounds { get; set; } = 2;
    public int RetryRoundDelaySeconds { get; set; } = 20;

    public int ItemsPerRequestFor(long windowSeconds) =>
        windowSeconds > (long)TimeSpan.FromDays(LargeWindowThresholdDays).TotalSeconds
            ? LargeWindowItemsPerRequest
            : ItemsPerRequest;

    public TimeSpan RequestTimeoutFor(long windowSeconds)
    {
        var windowDays = windowSeconds / TimeSpan.FromDays(1).TotalSeconds;
        var seconds = BaseRequestTimeoutSeconds + (PerWindowDayTimeoutSeconds * windowDays);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRequestTimeoutSeconds));
    }
}
