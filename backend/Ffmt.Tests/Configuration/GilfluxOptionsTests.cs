using FluentAssertions;
using Ffmt.Core.Configuration;
using Xunit;

namespace Ffmt.Tests.Configuration;

public sealed class GilfluxOptionsTests
{
    [Fact]
    public void DefaultTimeframes_SortedByDurationAscending()
    {
        var opts = new GilfluxOptions();
        var sorted = opts.TimeframesMs.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        sorted.Should().Equal("1h", "3h", "6h", "12h", "1d", "3d", "7d");
    }
}
