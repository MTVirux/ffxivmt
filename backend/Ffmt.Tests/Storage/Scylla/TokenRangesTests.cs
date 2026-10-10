using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class TokenRangesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    public void Ranges_cover_the_whole_ring_without_gaps(int count)
    {
        var ranges = TokenRanges.Split(count);

        ranges.Should().HaveCount(count);
        ranges[0].Start.Should().Be(long.MinValue);
        ranges[^1].End.Should().Be(long.MaxValue);
        for (var i = 1; i < ranges.Count; i++)
        {
            ranges[i].Start.Should().Be(ranges[i - 1].End);
            ranges[i].End.Should().BeGreaterThan(ranges[i].Start);
        }
    }
}
