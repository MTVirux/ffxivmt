using Ffmt.Core.Quarantine;

namespace Ffmt.Tests.Quarantine;

public sealed class PriceMedianTests
{
    [Fact]
    public void Odd_count_returns_the_middle_element()
    {
        PriceMedian.Compute([5, 1, 3]).Should().Be(3);
    }

    [Fact]
    public void Even_count_returns_the_lower_median_not_an_average()
    {
        PriceMedian.Compute([10, 20, 30, 40]).Should().Be(20,
            "the lower median keeps the result an exact observed price and avoids integer rounding");
    }

    [Fact]
    public void Single_sample_returns_that_sample()
    {
        PriceMedian.Compute([777]).Should().Be(777);
    }

    [Fact]
    public void All_identical_returns_that_value()
    {
        PriceMedian.Compute([42, 42, 42, 42]).Should().Be(42);
    }

    [Fact]
    public void Empty_returns_zero()
    {
        PriceMedian.Compute([]).Should().Be(0);
    }

    [Fact]
    public void A_minority_of_extreme_outliers_does_not_move_the_median()
    {
        PriceMedian.Compute([100, 110, 120, 130, 999_999_999]).Should().Be(120,
            "this is the property that makes the baseline self-protecting against the trades it is meant to catch");
    }

    [Fact]
    public void Does_not_mutate_the_caller_list()
    {
        var input = new[] { 5, 1, 3 };
        PriceMedian.Compute(input);
        input.Should().Equal(5, 1, 3);
    }
}
