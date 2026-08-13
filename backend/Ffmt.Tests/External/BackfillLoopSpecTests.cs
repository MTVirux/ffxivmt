using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.Models;

namespace Ffmt.Tests.External;

/// <summary>Window and pointer rules for the two backfill loops. Getting these wrong either
/// re-imports the same window forever or silently skips days.</summary>
public sealed class BackfillLoopSpecTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly BackfillOptions Options = new()
    {
        ChunkDays = 7,
        SkipIfGapUnderMinutes = 5,
    };

    private static BackfillBucketState State(DateTimeOffset? last, DateTimeOffset? earliest) =>
        new("Europe", "loop", 3, last, earliest, CrawlComplete: false);

    [Fact]
    public void Live_asks_for_everything_since_the_last_import()
    {
        var last = Now.AddHours(-2);

        var window = BackfillLoopSpec.Live.SelectWindow(Options, State(last, null), Now);

        window.Should().NotBeNull();
        window!.Value.Start.Should().Be(last);
        window.Value.End.Should().Be(Now);
        window.Value.OlderThan.Should().BeNull("the live loop keeps every row the window returns");
    }

    [Fact]
    public void Live_skips_a_bucket_whose_gap_is_under_the_threshold()
    {
        BackfillLoopSpec.Live.SelectWindow(Options, State(Now.AddMinutes(-1), null), Now).Should().BeNull();
    }

    [Fact]
    public void Live_skips_a_bucket_that_has_no_pointer_yet()
    {
        BackfillLoopSpec.Live.SelectWindow(Options, State(null, null), Now).Should().BeNull(
            "a missing pointer seeds from now, which is a zero-length gap");
    }

    [Fact]
    public void Live_moves_its_pointer_to_now_even_when_the_window_held_nothing()
    {
        var state = State(Now.AddHours(-2), null);
        var window = BackfillLoopSpec.Live.SelectWindow(Options, state, Now)!.Value;

        var (next, outcome) = BackfillLoopSpec.Live.Advance(state, window, gotRows: false);

        next.LastImportAt.Should().Be(Now);
        next.CrawlComplete.Should().BeFalse();
        outcome.Should().Be(BackfillBucketOutcome.Advanced);
    }

    [Fact]
    public void Historical_walks_back_one_chunk_from_the_earliest_import()
    {
        var earliest = Now.AddDays(-30);

        var window = BackfillLoopSpec.Historical.SelectWindow(Options, State(null, earliest), Now)!.Value;

        window.Start.Should().Be(earliest.AddDays(-7));
        window.End.Should().Be(Now);
        window.OlderThan.Should().Be(earliest, "rows newer than the previous window were already imported");
    }

    [Fact]
    public void Historical_seeds_its_first_window_from_now()
    {
        var window = BackfillLoopSpec.Historical.SelectWindow(Options, State(null, null), Now)!.Value;

        window.Start.Should().Be(Now.AddDays(-7));
        window.OlderThan.Should().Be(Now);
    }

    [Fact]
    public void Historical_never_skips_a_bucket()
    {
        BackfillLoopSpec.Historical.SelectWindow(Options, State(Now, Now), Now).Should().NotBeNull();
    }

    [Fact]
    public void Historical_advances_its_pointer_to_the_window_start_when_rows_came_back()
    {
        var earliest = Now.AddDays(-30);
        var state = State(null, earliest);
        var window = BackfillLoopSpec.Historical.SelectWindow(Options, state, Now)!.Value;

        var (next, outcome) = BackfillLoopSpec.Historical.Advance(state, window, gotRows: true);

        next.EarliestImportAt.Should().Be(earliest.AddDays(-7));
        next.CrawlComplete.Should().BeFalse();
        outcome.Should().Be(BackfillBucketOutcome.Advanced);
    }

    [Fact]
    public void Historical_marks_the_crawl_complete_when_the_window_held_nothing()
    {
        var earliest = Now.AddDays(-30);
        var state = State(null, earliest);
        var window = BackfillLoopSpec.Historical.SelectWindow(Options, state, Now)!.Value;

        var (next, outcome) = BackfillLoopSpec.Historical.Advance(state, window, gotRows: false);

        next.CrawlComplete.Should().BeTrue();
        next.EarliestImportAt.Should().Be(earliest, "a finished bucket leaves its pointer where it is");
        outcome.Should().Be(BackfillBucketOutcome.Complete);
    }

    [Fact]
    public void Only_the_historical_loop_reports_history_depth()
    {
        BackfillLoopSpec.Historical.TracksHistoryDepth.Should().BeTrue();
        BackfillLoopSpec.Live.TracksHistoryDepth.Should().BeFalse();
        BackfillLoopSpec.Historical.Name.Should().Be(BackfillLoops.Historical);
        BackfillLoopSpec.Live.Name.Should().Be(BackfillLoops.Live);
    }
}
