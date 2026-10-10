using Ffmt.Core.Mannequin;

namespace Ffmt.Tests.Mannequin;

public sealed class MannequinCapTests
{
    private static readonly DateOnly D1 = new(2026, 10, 1);
    private static readonly DateOnly D2 = new(2026, 10, 2);
    private static readonly DateOnly D3 = new(2026, 10, 3);

    private static Dictionary<DateOnly, long> Rows(params (DateOnly Day, long Rows)[] days) =>
        days.ToDictionary(d => d.Day, d => d.Rows);

    [Fact]
    public void Nothing_drops_under_the_cap() =>
        MannequinCap.DaysToDrop(Rows((D1, 10), (D2, 10)), bytesPerRow: 100, maxBytes: 10_000).Should().BeEmpty();

    [Fact]
    public void Exactly_at_the_cap_keeps_everything() =>
        MannequinCap.DaysToDrop(Rows((D1, 50), (D2, 50)), bytesPerRow: 100, maxBytes: 10_000).Should().BeEmpty();

    [Fact]
    public void Oldest_days_drop_first_until_under_the_cap() =>
        MannequinCap.DaysToDrop(Rows((D3, 50), (D1, 50), (D2, 50)), bytesPerRow: 100, maxBytes: 9_000)
            .Should().Equal(D1, D2);

    [Fact]
    public void The_newest_day_is_never_dropped() =>
        MannequinCap.DaysToDrop(Rows((D1, 10), (D2, 1_000)), bytesPerRow: 100, maxBytes: 1_000)
            .Should().Equal(D1);
}
