using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class BackfillTuningTests
{
    private static readonly BackfillTuning Tuning = new();

    private static long Seconds(TimeSpan span) => (long)span.TotalSeconds;

    [Fact]
    public void Request_timeout_grows_with_the_requested_window()
    {
        var oneHour = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromHours(1)));
        var nineDays = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(9)));

        nineDays.Should().BeGreaterThan(oneHour,
            "Universalis response size scales with the requested window, so a wider window needs longer");
    }

    [Fact]
    public void Request_timeout_scales_by_the_hour_not_only_by_the_day()
    {
        // A 4h live-gap window timed out at 63s in production: per-day scaling added only ~2.5s
        // on top of the base budget, so the window may as well not have been accounted for.
        Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromHours(4)))
            .Should().Be(TimeSpan.FromSeconds(
                Tuning.BaseRequestTimeoutSeconds + (Tuning.PerWindowHourTimeoutSeconds * 4)));
    }

    [Fact]
    public void Request_timeout_is_clamped_at_the_maximum()
    {
        Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(365)))
            .Should().Be(TimeSpan.FromSeconds(Tuning.MaxRequestTimeoutSeconds));
    }

    [Fact]
    public void Narrow_windows_keep_the_full_item_batch()
    {
        Tuning.ItemsPerRequestFor(Seconds(TimeSpan.FromMinutes(15)))
            .Should().Be(Tuning.ItemsPerRequest,
            "a caught-up live gap is small enough that the full batch keeps the request count down");
    }

    [Fact]
    public void Multi_hour_windows_use_a_smaller_item_batch()
    {
        // Batch size dominates timeout risk over window width: in production a 9-day window at the
        // small batch never timed out, while a 4h window at the full batch timed out every pass.
        Tuning.ItemsPerRequestFor(Seconds(TimeSpan.FromHours(4)))
            .Should().Be(Tuning.LargeWindowItemsPerRequest);
    }

    [Fact]
    public void Wide_windows_use_a_smaller_item_batch()
    {
        Tuning.ItemsPerRequestFor(Seconds(TimeSpan.FromDays(9)))
            .Should().Be(Tuning.LargeWindowItemsPerRequest);
    }

    [Fact]
    public void Concurrency_covers_a_full_pass_at_the_smaller_batch_within_the_crawl_interval()
    {
        // 16842 marketable items at the smaller batch is ~1123 requests. At concurrency 4 that
        // pass takes hours and never completes inside the hourly crawl interval.
        const int RequestsPerPass = 16842 / 15;
        var perRequestSeconds = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(9))).TotalSeconds / 10;
        var passMinutes = RequestsPerPass * perRequestSeconds / Tuning.Concurrency / 60;

        passMinutes.Should().BeLessThan(60,
            "a pass that outlasts the crawl interval can never advance its pointer");
    }
}
