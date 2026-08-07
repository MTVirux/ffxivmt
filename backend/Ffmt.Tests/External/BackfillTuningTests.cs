using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class BackfillTuningTests
{
    private static readonly BackfillTuning Tuning = new();

    private static long Seconds(TimeSpan span) => (long)span.TotalSeconds;

    [Fact]
    public void Request_timeout_grows_with_the_requested_window()
    {
        var oneDay = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(1)));
        var nineDays = Tuning.RequestTimeoutFor(Seconds(TimeSpan.FromDays(9)));

        nineDays.Should().BeGreaterThan(oneDay,
            "Universalis response size scales with the requested window, so a wider window needs longer");
        nineDays.Should().Be(TimeSpan.FromSeconds(
            Tuning.BaseRequestTimeoutSeconds + (Tuning.PerWindowDayTimeoutSeconds * 9)));
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
            .Should().Be(Tuning.ItemsPerRequest);
    }

    [Fact]
    public void Wide_windows_use_a_smaller_item_batch()
    {
        Tuning.ItemsPerRequestFor(Seconds(TimeSpan.FromDays(9)))
            .Should().Be(Tuning.LargeWindowItemsPerRequest,
            "a 9-day window at the full batch size is what drove the request timeouts");
    }
}
