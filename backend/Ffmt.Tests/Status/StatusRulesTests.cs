using Ffmt.Core.Status;

namespace Ffmt.Tests.Status;

public sealed class StatusRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void No_stored_sales_is_down(double? storedBatches)
    {
        var verdict = StatusRules.Evaluate(storedBatches, 0.0, 80, 80);

        verdict.State.Should().Be("down");
        verdict.Reasons.Should().Equal("No sales stored in the last 10 minutes");
    }

    [Fact]
    public void Healthy_is_operational()
    {
        var verdict = StatusRules.Evaluate(500, 0.01, 80, 80);

        verdict.State.Should().Be("operational");
        verdict.Reasons.Should().BeEmpty();
    }

    [Fact]
    public void High_error_rate_is_degraded()
    {
        var verdict = StatusRules.Evaluate(500, 0.072, 80, 80);

        verdict.State.Should().Be("degraded");
        verdict.Reasons.Should().Equal("5xx error rate 7.2% (above 5%)");
    }

    [Fact]
    public void Few_worlds_connected_is_degraded()
    {
        var verdict = StatusRules.Evaluate(500, 0.0, 71, 80);

        verdict.State.Should().Be("degraded");
        verdict.Reasons.Should().Equal("71 of 80 worlds connected");
    }

    [Fact]
    public void All_degraded_reasons_are_listed()
    {
        StatusRules.Evaluate(500, 0.2, 10, 80).Reasons.Should().HaveCount(2);
    }

    [Fact]
    public void Thresholds_are_exclusive()
    {
        StatusRules.Evaluate(500, 0.05, 72, 80).State.Should().Be("operational");
    }

    [Fact]
    public void Missing_error_rate_and_world_counts_do_not_degrade()
    {
        StatusRules.Evaluate(500, null, null, null).State.Should().Be("operational");
    }
}
