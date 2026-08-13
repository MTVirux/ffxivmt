using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class BackfillBucketsTests
{
    private const int MarketableItems = 16842;

    [Fact]
    public void Every_item_lands_in_exactly_one_bucket()
    {
        var items = Enumerable.Range(1, 5000).ToList();

        var grouped = BackfillBuckets.Group(items, bucketCount: 100);

        grouped.Values.Sum(v => v.Count).Should().Be(items.Count);
        grouped.Values.SelectMany(v => v).Distinct().Should().HaveCount(items.Count);
    }

    [Fact]
    public void Bucket_membership_does_not_shift_when_new_items_appear()
    {
        // Universalis adds items over time. If membership shifted, a bucket's pointer would no
        // longer describe the items it covers, and history would be silently skipped.
        var before = BackfillBuckets.Group([10, 20, 30], bucketCount: 7);
        var after = BackfillBuckets.Group([10, 20, 30, 40, 50], bucketCount: 7);

        foreach (var id in new[] { 10, 20, 30 })
        {
            var wasIn = before.Single(kv => kv.Value.Contains(id)).Key;
            var nowIn = after.Single(kv => kv.Value.Contains(id)).Key;
            nowIn.Should().Be(wasIn);
        }
    }

    [Fact]
    public void Buckets_hold_about_one_request_worth_of_items()
    {
        var items = Enumerable.Range(1, MarketableItems).ToList();
        var bucketCount = BackfillBuckets.BucketCountFor(items.Count, itemsPerRequest: 50);

        var grouped = BackfillBuckets.Group(items, bucketCount);

        grouped.Values.Max(v => v.Count).Should().BeLessThanOrEqualTo(60,
            "a bucket is meant to be a single request; oversized buckets bring back the failures");
        grouped.Should().HaveCount(bucketCount);
    }

    [Fact]
    public void Empty_buckets_are_omitted_rather_than_returned_empty()
    {
        var grouped = BackfillBuckets.Group([5], bucketCount: 50);

        grouped.Should().HaveCount(1, "a pass must not issue requests for buckets holding no items");
    }
}

public sealed class BackfillWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Historical_window_steps_back_by_the_chunk_size()
    {
        var earliest = new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

        BackfillWindow.HistoricalStart(earliest, chunkDays: 7)
            .Should().Be(new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Entries_within_is_measured_from_now_not_from_the_window_width()
    {
        // The history endpoint only accepts a window relative to now, so a backwards crawl asks
        // for everything since the window start; the window's own width would skip the older half.
        var windowStart = Now.AddDays(-9);

        BackfillWindow.EntriesWithinSeconds(windowStart, Now)
            .Should().Be((long)TimeSpan.FromDays(9).TotalSeconds);
    }
}
