using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;

namespace Ffmt.Tests.Gilflux;

public sealed class RankingDecayTests
{
    private static readonly IReadOnlyDictionary<string, long> Timeframes = new GilfluxOptions().TimeframesMs;
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, long> Frozen() => new()
    {
        ["1h"] = 100,
        ["3h"] = 100,
        ["6h"] = 100,
        ["12h"] = 100,
        ["1d"] = 100,
        ["3d"] = 100,
        ["7d"] = 100,
    };

    // updated_at and last_sale share one timestamp: the row was last refreshed by its own last sale.
    private static IReadOnlyDictionary<string, long> DecayFrozenAt(DateTimeOffset lastTouched) =>
        RankingDecay.Apply(Frozen(), Timeframes, lastTouched.ToUnixTimeMilliseconds(), lastTouched.ToUnixTimeMilliseconds(), Now);

    private static GilfluxRanking Row(int worldId, int itemId, DateTimeOffset updatedAt, long value = 100, DateTimeOffset? lastSale = null) =>
        new(itemId, worldId,
            new Dictionary<string, long> { ["1h"] = value, ["3h"] = value, ["6h"] = value, ["12h"] = value, ["1d"] = value, ["3d"] = value, ["7d"] = value },
            updatedAt.ToUnixTimeMilliseconds(),
            (lastSale ?? updatedAt).ToUnixTimeMilliseconds());

    [Fact]
    public void Apply_ZeroesEveryTimeframeShorterThanTheRowsAge()
    {
        var decayed = DecayFrozenAt(Now.AddHours(-4));

        decayed["1h"].Should().Be(0);
        decayed["3h"].Should().Be(0);
        decayed["6h"].Should().Be(100);
        decayed["7d"].Should().Be(100);
    }

    [Fact]
    public void Apply_LeavesAJustRefreshedRowUntouched()
    {
        DecayFrozenAt(Now.AddMinutes(-1)).Should().BeEquivalentTo(Frozen());
    }

    [Fact]
    public void Apply_ZeroesEverythingOnARowOlderThanTheWidestTimeframe()
    {
        var decayed = DecayFrozenAt(Now.AddDays(-66));

        decayed.Values.Should().OnlyContain(v => v == 0);
        RankingDecay.IsExhausted(decayed).Should().BeTrue();
    }

    [Fact]
    public void Apply_ZeroesEverythingWhenUpdatedAtIsMissing()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, null, Now.ToUnixTimeMilliseconds(), Now);

        decayed.Values.Should().OnlyContain(v => v == 0);
    }

    [Fact]
    public void Apply_ZeroesEverythingWhenTheLastSaleHasAgedOutEvenThoughTheRowLooksFresh()
    {
        var decayed = RankingDecay.Apply(
            Frozen(), Timeframes,
            Now.AddHours(-1).ToUnixTimeMilliseconds(),
            Now.AddDays(-13).ToUnixTimeMilliseconds(),
            Now);

        decayed.Values.Should().OnlyContain(v => v == 0);
    }

    [Fact]
    public void Apply_ZeroesOnlyTheTimeframesTheLastSaleHasFallenOutOf()
    {
        var decayed = RankingDecay.Apply(
            Frozen(), Timeframes,
            Now.AddMinutes(-1).ToUnixTimeMilliseconds(),
            Now.AddHours(-4).ToUnixTimeMilliseconds(),
            Now);

        decayed["1h"].Should().Be(0);
        decayed["3h"].Should().Be(0);
        decayed["6h"].Should().Be(100);
    }

    [Fact]
    public void Apply_TreatsTheLegacyEpochZeroLastSaleAsNoSaleAtAll()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, Now.ToUnixTimeMilliseconds(), 0L, Now);

        decayed.Values.Should().OnlyContain(v => v == 0);
    }

    [Fact]
    public void Apply_ZeroesTimeframesAbsentFromConfiguration()
    {
        var stored = new Dictionary<string, long> { ["1h"] = 100, ["30d"] = 500 };

        var decayed = RankingDecay.Apply(stored, Timeframes, Now.ToUnixTimeMilliseconds(), Now.ToUnixTimeMilliseconds(), Now);

        decayed["1h"].Should().Be(100);
        decayed["30d"].Should().Be(0);
    }

    [Fact]
    public void Plan_DeletesRowsOlderThanTheWidestTimeframeWithoutRefreshingThem()
    {
        var rows = new[] { Row(21, 5057, Now.AddDays(-66)) };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 100, Now);

        plan.Delete.Should().ContainSingle().Which.Should().Be((21, 5057));
        plan.Refresh.Should().BeEmpty();
    }

    [Fact]
    public void Plan_DeletesRowsWhoseLastSaleHasAgedOutOfEveryTimeframe()
    {
        var rows = new[] { Row(21, 5057, Now.AddDays(-6), lastSale: Now.AddDays(-13)) };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 100, Now);

        plan.Delete.Should().ContainSingle().Which.Should().Be((21, 5057));
        plan.Refresh.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RefreshesStaleRowsThatStillHaveValueLeft()
    {
        var rows = new[] { Row(21, 5057, Now.AddHours(-4)) };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 100, Now);

        plan.Delete.Should().BeEmpty();
        plan.Refresh.Should().ContainSingle().Which.Should().Be((21, 5057));
    }

    [Fact]
    public void Plan_LeavesRowsRefreshedInsideTheStalenessWindowAlone()
    {
        var rows = new[] { Row(21, 5057, Now.AddMinutes(-1)) };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 100, Now);

        plan.Delete.Should().BeEmpty();
        plan.Refresh.Should().BeEmpty();
    }

    [Fact]
    public void Plan_CapsRefreshesAndTakesTheStalestFirst()
    {
        var rows = new[]
        {
            Row(21, 1, Now.AddHours(-2)),
            Row(21, 2, Now.AddHours(-9)),
            Row(21, 3, Now.AddHours(-5)),
        };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 2, Now);

        plan.Refresh.Should().Equal((21, 2), (21, 3));
    }

    [Fact]
    public void Plan_DeletesRowsWhoseStoredValuesAreAlreadyAllZero()
    {
        var rows = new[] { Row(21, 5057, Now.AddMinutes(-1), value: 0) };

        var plan = RankingDecay.Plan(rows, Timeframes, TimeSpan.FromMinutes(15), maxRefresh: 100, Now);

        plan.Delete.Should().ContainSingle().Which.Should().Be((21, 5057));
        plan.Refresh.Should().BeEmpty();
    }
}
