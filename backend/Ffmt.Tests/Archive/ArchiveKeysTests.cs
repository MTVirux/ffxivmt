using Ffmt.Core.Archive;

namespace Ffmt.Tests.Archive;

/// <summary>Pins the S3 layout against the literals the archive commands used before
/// <see cref="ArchiveKeys"/> existed. A diff here orphans every object already in the bucket.</summary>
public sealed class ArchiveKeysTests
{
    private static string LegacyArchiveKey(DateOnly date, string dc, string world) =>
        $"archive/{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";

    private static string LegacyCorrectionsKey(DateOnly date, string dc, string world) =>
        $"corrections/{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";

    [Theory]
    [InlineData(2026, 5, 1, "Chaos", "Ravana")]
    [InlineData(2026, 12, 31, "Light", "Alpha")]
    [InlineData(2025, 1, 9, "Aether", "Sargatanas")]
    public void For_matches_the_legacy_literals(int year, int month, int day, string dc, string world)
    {
        var date = new DateOnly(year, month, day);

        ArchiveKeys.For(ArchiveKeys.ArchivePrefix, date, dc, world)
            .Should().Be(LegacyArchiveKey(date, dc, world));
        ArchiveKeys.For(ArchiveKeys.CorrectionsPrefix, date, dc, world)
            .Should().Be(LegacyCorrectionsKey(date, dc, world));
    }

    [Fact]
    public void For_pads_month_and_day_to_two_digits()
    {
        ArchiveKeys.For(ArchiveKeys.ArchivePrefix, new DateOnly(2026, 5, 1), "Chaos", "Ravana")
            .Should().Be("archive/2026/05/01/Chaos/Ravana.parquet");
    }

    [Fact]
    public void ToArchiveKey_matches_the_legacy_prefix_swap()
    {
        const string corrKey = "corrections/2026/05/01/Chaos/Ravana.parquet";

        ArchiveKeys.ToArchiveKey(corrKey)
            .Should().Be("archive/" + corrKey["corrections/".Length..])
            .And.Be("archive/2026/05/01/Chaos/Ravana.parquet");
    }
}
