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

    private static GilfluxRanking Row(int worldId, int itemId, DateTimeOffset updatedAt, long value = 100) =>
        new(itemId, worldId,
            new Dictionary<string, long> { ["1h"] = value, ["3h"] = value, ["6h"] = value, ["12h"] = value, ["1d"] = value, ["3d"] = value, ["7d"] = value },
            updatedAt.ToUnixTimeMilliseconds(),
            updatedAt.ToUnixTimeMilliseconds());

    [Fact]
    public void Apply_ZeroesEveryTimeframeShorterThanTheRowsAge()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, Now.AddHours(-4).ToUnixTimeMilliseconds(), Now);

        decayed["1h"].Should().Be(0);
        decayed["3h"].Should().Be(0);
        decayed["6h"].Should().Be(100);
        decayed["7d"].Should().Be(100);
    }

    [Fact]
    public void Apply_LeavesAJustRefreshedRowUntouched()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, Now.AddMinutes(-1).ToUnixTimeMilliseconds(), Now);

        decayed.Should().BeEquivalentTo(Frozen());
    }

    [Fact]
    public void Apply_ZeroesEverythingOnARowOlderThanTheWidestTimeframe()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, Now.AddDays(-66).ToUnixTimeMilliseconds(), Now);

        decayed.Values.Should().OnlyContain(v => v == 0);
        RankingDecay.IsExhausted(decayed).Should().BeTrue();
    }

    [Fact]
    public void Apply_ZeroesEverythingWhenUpdatedAtIsMissing()
    {
        var decayed = RankingDecay.Apply(Frozen(), Timeframes, null, Now);

        decayed.Values.Should().OnlyContain(v => v == 0);
    }

    [Fact]
    public void Apply_ZeroesTimeframesAbsentFromConfiguration()
    {
        var stored = new Dictionary<string, long> { ["1h"] = 100, ["30d"] = 500 };

        var decayed = RankingDecay.Apply(stored, Timeframes, Now.ToUnixTimeMilliseconds(), Now);

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
