using Ffmt.Core.Configuration;
using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>
/// <c>Skipped</c> and <c>Stalled</c> are set by the pass, not the spec: a bucket with nothing worth
/// asking for, and a bucket whose request never succeeded.
/// </summary>
public enum BackfillBucketOutcome
{
    Advanced,
    Stalled,
    Complete,
    Skipped,
}

/// <summary>The span a bucket asks Universalis for.</summary>
public readonly record struct BackfillBucketWindow(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// All that separates the live-gap loop from the historical crawl: which window a bucket asks for
/// next, and where its pointer lands afterwards. Everything else in a pass is shared.
/// </summary>
public sealed class BackfillLoopSpec
{
    private readonly Func<BackfillOptions, BackfillBucketState, DateTimeOffset, BackfillBucketWindow?> _selectWindow;
    private readonly Func<BackfillBucketState, BackfillBucketWindow, bool, DateTimeOffset?,
        (BackfillBucketState State, BackfillBucketOutcome Outcome)> _advance;

    private BackfillLoopSpec(
        string name,
        bool tracksHistoryDepth,
        bool looksPastEmptyWindows,
        Func<BackfillOptions, BackfillBucketState, DateTimeOffset, BackfillBucketWindow?> selectWindow,
        Func<BackfillBucketState, BackfillBucketWindow, bool, DateTimeOffset?,
            (BackfillBucketState State, BackfillBucketOutcome Outcome)> advance)
    {
        Name = name;
        TracksHistoryDepth = tracksHistoryDepth;
        LooksPastEmptyWindows = looksPastEmptyWindows;
        _selectWindow = selectWindow;
        _advance = advance;
    }

    public string Name { get; }

    public bool TracksHistoryDepth { get; }

    /// <summary>Whether an empty window needs the newest sale older than it before the bucket can
    /// advance.</summary>
    public bool LooksPastEmptyWindows { get; }

    /// <summary>Null when this bucket has nothing worth asking for on this pass.</summary>
    public BackfillBucketWindow? SelectWindow(BackfillOptions options, BackfillBucketState state, DateTimeOffset now) =>
        _selectWindow(options, state, now);

    /// <summary><paramref name="newestOlderSale"/> is only looked up for an empty window of a loop that
    /// <see cref="LooksPastEmptyWindows"/>, and is null when nothing older exists.</summary>
    public (BackfillBucketState State, BackfillBucketOutcome Outcome) Advance(
        BackfillBucketState state, BackfillBucketWindow window, bool gotRows, DateTimeOffset? newestOlderSale = null) =>
        _advance(state, window, gotRows, newestOlderSale);

    /// <summary>Closes the gap between the newest imported sale and now. Buckets touched recently
    /// are left alone so a short pass interval does not re-ask for the same minutes.</summary>
    public static readonly BackfillLoopSpec Live = new(
        BackfillLoops.Live,
        tracksHistoryDepth: false,
        looksPastEmptyWindows: false,
        (options, state, now) =>
        {
            var last = state.LastImportAt ?? now;
            return now - last < TimeSpan.FromMinutes(options.SkipIfGapUnderMinutes)
                ? null
                : new BackfillBucketWindow(last, now);
        },
        (state, window, _, _) => (state with { LastImportAt = window.End }, BackfillBucketOutcome.Advanced));

    /// <summary>Walks backwards one chunk at a time. Rarely traded items can go weeks without a sale,
    /// so an empty window jumps the pointer to the newest older sale instead of ending the crawl;
    /// only finding nothing older does that.</summary>
    public static readonly BackfillLoopSpec Historical = new(
        BackfillLoops.Historical,
        tracksHistoryDepth: true,
        looksPastEmptyWindows: true,
        (options, state, now) =>
        {
            var earliest = state.EarliestImportAt ?? now;
            return new BackfillBucketWindow(BackfillWindow.HistoricalStart(earliest, options.ChunkDays), earliest);
        },
        (state, window, gotRows, newestOlderSale) => (gotRows, newestOlderSale) switch
        {
            (true, _) => (state with { EarliestImportAt = window.Start }, BackfillBucketOutcome.Advanced),
            (false, { } older) => (state with { EarliestImportAt = older }, BackfillBucketOutcome.Advanced),
            _ => (state with { CrawlComplete = true }, BackfillBucketOutcome.Complete),
        });
}
