using Ffmt.Core.Configuration;
using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>
/// How one bucket ended a pass. <c>Skipped</c> and <c>Stalled</c> are decided by the pass itself -
/// a bucket with nothing worth asking for, and a bucket whose request never succeeded.
/// </summary>
public enum BackfillBucketOutcome
{
    Advanced,
    Stalled,
    Complete,
    Skipped,
}

/// <summary>
/// The span a bucket asks Universalis for. The endpoint only takes a window relative to now, so a
/// crawl walking backwards asks for everything since <paramref name="Start"/> and discards rows at
/// or after <paramref name="OlderThan"/>, which is where the previous window began.
/// </summary>
public readonly record struct BackfillBucketWindow(
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset? OlderThan);

/// <summary>
/// All that separates the live-gap loop from the historical crawl: which window a bucket asks for
/// next, and where its pointer lands afterwards. Everything else in a pass is shared.
/// </summary>
public sealed class BackfillLoopSpec
{
    private readonly Func<BackfillOptions, BackfillBucketState, DateTimeOffset, BackfillBucketWindow?> _selectWindow;
    private readonly Func<BackfillBucketState, BackfillBucketWindow, bool,
        (BackfillBucketState State, BackfillBucketOutcome Outcome)> _advance;

    private BackfillLoopSpec(
        string name,
        bool tracksHistoryDepth,
        Func<BackfillOptions, BackfillBucketState, DateTimeOffset, BackfillBucketWindow?> selectWindow,
        Func<BackfillBucketState, BackfillBucketWindow, bool,
            (BackfillBucketState State, BackfillBucketOutcome Outcome)> advance)
    {
        Name = name;
        TracksHistoryDepth = tracksHistoryDepth;
        _selectWindow = selectWindow;
        _advance = advance;
    }

    public string Name { get; }

    public bool TracksHistoryDepth { get; }

    /// <summary>Null when this bucket has nothing worth asking for on this pass.</summary>
    public BackfillBucketWindow? SelectWindow(BackfillOptions options, BackfillBucketState state, DateTimeOffset now) =>
        _selectWindow(options, state, now);

    public (BackfillBucketState State, BackfillBucketOutcome Outcome) Advance(
        BackfillBucketState state, BackfillBucketWindow window, bool gotRows) =>
        _advance(state, window, gotRows);

    /// <summary>Closes the gap between the newest imported sale and now. Buckets touched recently
    /// are left alone so a short pass interval does not re-ask for the same minutes.</summary>
    public static readonly BackfillLoopSpec Live = new(
        BackfillLoops.Live,
        tracksHistoryDepth: false,
        (options, state, now) =>
        {
            var last = state.LastImportAt ?? now;
            return now - last < TimeSpan.FromMinutes(options.SkipIfGapUnderMinutes)
                ? null
                : new BackfillBucketWindow(last, now, OlderThan: null);
        },
        (state, window, _) => (state with { LastImportAt = window.End }, BackfillBucketOutcome.Advanced));

    /// <summary>Walks backwards one chunk at a time. A window that yields nothing is how the crawl
    /// learns it has reached the end of that bucket's history.</summary>
    public static readonly BackfillLoopSpec Historical = new(
        BackfillLoops.Historical,
        tracksHistoryDepth: true,
        (options, state, now) =>
        {
            var earliest = state.EarliestImportAt ?? now;
            return new BackfillBucketWindow(
                BackfillWindow.HistoricalStart(earliest, options.ChunkDays), now, OlderThan: earliest);
        },
        (state, window, gotRows) => gotRows
            ? (state with { EarliestImportAt = window.Start }, BackfillBucketOutcome.Advanced)
            : (state with { CrawlComplete = true }, BackfillBucketOutcome.Complete));
}
