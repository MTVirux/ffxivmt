using Ffmt.Core.External;
using Ffmt.Core.Models;

namespace Ffmt.Tests.External;

/// <summary>Every restart begins a new pass, so a fixed order starves the regions at the end of it.</summary>
public sealed class BackfillRegionOrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static (string, IReadOnlyList<BackfillBucketProgress>) Region(string name, params DateTimeOffset?[] written) =>
        (name, written.Select((at, bucket) => new BackfillBucketProgress(bucket, T0, CrawlComplete: false, at)).ToList());

    [Fact]
    public void The_region_whose_newest_bucket_advanced_longest_ago_goes_first()
    {
        var order = BackfillRegionOrder.StalestFirst(
        [
            Region("Europe", T0.AddMinutes(-5), T0.AddDays(-3)),
            Region("North-America", T0.AddDays(-2)),
            Region("Japan", T0.AddDays(-3), T0.AddHours(-1)),
            Region("Oceania", T0.AddDays(-1)),
        ]);

        order.Should().Equal("North-America", "Oceania", "Japan", "Europe");
    }

    [Fact]
    public void Regions_that_never_advanced_go_first()
    {
        var order = BackfillRegionOrder.StalestFirst(
        [
            Region("Europe", T0.AddDays(-3)),
            Region("North-America"),
            Region("Japan", null, null),
        ]);

        order.Should().Equal("North-America", "Japan", "Europe");
    }

    [Fact]
    public void Ties_keep_the_configured_order()
    {
        var order = BackfillRegionOrder.StalestFirst(
        [
            Region("Europe", T0),
            Region("North-America"),
            Region("Japan", T0),
            Region("Oceania"),
        ]);

        order.Should().Equal("North-America", "Oceania", "Europe", "Japan");
    }

    [Fact]
    public void No_regions_gives_an_empty_order()
    {
        BackfillRegionOrder.StalestFirst([]).Should().BeEmpty();
    }
}
