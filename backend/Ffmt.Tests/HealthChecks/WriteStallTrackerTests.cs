using Ffmt.Core.HealthChecks;

namespace Ffmt.Tests.HealthChecks;

public sealed class WriteStallTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(5);

    [Fact]
    public void Is_not_stalled_without_any_writes()
    {
        var tracker = new WriteStallTracker();

        tracker.IsStalled(Start, Threshold).Should().BeFalse();
    }

    [Fact]
    public void Is_stalled_when_failures_continue_past_the_threshold_without_a_success()
    {
        var tracker = new WriteStallTracker();
        tracker.RecordSuccess(Start);
        for (var minute = 1; minute <= 7; minute++)
            tracker.RecordFailure(Start.AddMinutes(minute));

        tracker.IsStalled(Start.AddMinutes(7), Threshold).Should().BeTrue(
            "every insert has failed for six minutes and they are still failing");
    }

    [Fact]
    public void Is_not_stalled_when_failures_have_lasted_less_than_the_threshold()
    {
        var tracker = new WriteStallTracker();
        tracker.RecordFailure(Start);
        tracker.RecordFailure(Start.AddMinutes(2));
        tracker.RecordFailure(Start.AddMinutes(4));

        tracker.IsStalled(Start.AddMinutes(4), Threshold).Should().BeFalse();
    }

    [Fact]
    public void A_success_after_failures_resets_the_streak()
    {
        var tracker = new WriteStallTracker();
        for (var minute = 0; minute <= 4; minute++)
            tracker.RecordFailure(Start.AddMinutes(minute));
        tracker.RecordSuccess(Start.AddMinutes(4.5));
        for (var minute = 5; minute <= 8; minute++)
            tracker.RecordFailure(Start.AddMinutes(minute));

        tracker.IsStalled(Start.AddMinutes(8), Threshold).Should().BeFalse(
            "the streak restarted at minute 5, only three minutes ago");
    }

    [Fact]
    public void Is_not_stalled_when_a_single_failure_is_followed_by_silence()
    {
        var tracker = new WriteStallTracker();
        tracker.RecordFailure(Start);

        tracker.IsStalled(Start.AddMinutes(10), Threshold).Should().BeFalse(
            "a quiet period with no inserts says nothing about whether writes still fail");
    }
}
