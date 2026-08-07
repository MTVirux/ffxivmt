using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class BackfillTuningTests
{
    private const int MarketableItems = 16842;

    // Measured directly against the Universalis history endpoint: a 15-item request covering a
    // 9-day window and returning 2.9MB of JSON came back in 3.2s. Request latency is small; the
    // failures this tuning has to respect are 504s under load, not slow responses.
    private const double ObservedSecondsPerRequest = 5;

    private static readonly BackfillTuning Tuning = new();

    private static long Seconds(TimeSpan span) => (long)span.TotalSeconds;

    [Fact]
    public void Request_timeout_grows_with_the_requested_window()
    {
        var oneHour = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromHours(1)));
        var nineDays = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(9)));

        nineDays.Should().BeGreaterThan(oneHour,
            "a wider window returns more JSON, so it is allowed longer");
    }

    [Fact]
    public void Request_timeout_is_clamped_at_the_maximum()
    {
        Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(365)))
            .Should().Be(TimeSpan.FromSeconds(Tuning.MaxRequestTimeoutSeconds));
    }

    [Fact]
    public void Concurrency_stays_within_what_universalis_tolerates()
    {
        // Raising this to 16 produced sustained 504s from Universalis, which the retry policy
        // then masked as client timeouts. Both loops run their own pass, so the load on the
        // upstream API is double this figure.
        Tuning.Concurrency.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void A_full_pass_fits_inside_the_crawl_interval()
    {
        var requests = Math.Ceiling(MarketableItems / (double)Tuning.ItemsPerRequest);
        var passMinutes = requests * ObservedSecondsPerRequest / Tuning.Concurrency / 60;

        passMinutes.Should().BeLessThan(60,
            "a pass that outlasts the crawl interval can never advance its pointer");
    }
}
