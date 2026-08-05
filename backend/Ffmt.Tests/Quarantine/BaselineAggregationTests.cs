using Ffmt.Cli.Commands;
using Ffmt.Core.Quarantine;

namespace Ffmt.Tests.Quarantine;

public sealed class BaselineAggregationTests
{
    [Fact]
    public void Aggregate_splits_hq_from_nq()
    {
        PricePoint[] points =
        [
            new(false, 100), new(false, 200), new(false, 300),
            new(true, 10_000), new(true, 20_000),
        ];

        UpdateBaselinesCommand.Aggregate(points, hq: false).Should().Be((200L, 3));
        UpdateBaselinesCommand.Aggregate(points, hq: true).Should().Be((10_000L, 2));
    }

    [Fact]
    public void Aggregate_returns_zero_count_when_the_slice_is_empty()
    {
        UpdateBaselinesCommand.Aggregate([new PricePoint(false, 100)], hq: true).Should().Be((0L, 0));
    }
}
