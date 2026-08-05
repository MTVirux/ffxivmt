using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Quarantine;

public sealed class PriceBaselineProviderTests
{
    private static (PriceBaselineProvider Provider, List<string> Captured) NewProvider()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));

        var provider = new PriceBaselineProvider(
            session,
            Options.Create(new UniversalisOptions()),
            Options.Create(new QuarantineOptions()),
            NullLogger<PriceBaselineProvider>.Instance);

        return (provider, captured);
    }

    [Fact]
    public async Task EnsureLoadedAsync_selects_the_baseline_table_partitioned_by_region()
    {
        var (provider, captured) = NewProvider();

        await provider.EnsureLoadedAsync();

        captured.Should().Contain(c =>
            c.Contains("FROM item_price_baseline") && c.Contains("region = ?"));
    }

    [Fact]
    public async Task A_failed_load_leaves_an_empty_snapshot_and_does_not_throw()
    {
        var (provider, _) = NewProvider();

        await provider.EnsureLoadedAsync();

        provider.TryGet(12345, "Europe", false, out _).Should().BeFalse(
            "a baseline that cannot be read must fail open, never throw into the ingest path");
    }

    [Fact]
    public void TryGet_returns_a_loaded_baseline_and_misses_on_a_different_hq_flag()
    {
        var (provider, _) = NewProvider();
        provider.SetSnapshotForTest(new Dictionary<(int, string, bool), PriceBaseline>
        {
            [(12345, "Europe", false)] = new(1_000, 50, DateTimeOffset.UnixEpoch),
        });

        provider.TryGet(12345, "Europe", false, out var nq).Should().BeTrue();
        nq!.MedianUnitPrice.Should().Be(1_000);

        provider.TryGet(12345, "Europe", true, out _).Should().BeFalse(
            "hq and nq are separate slices - a shared baseline would false-positive on legitimate hq sales");
        provider.TryGet(12345, "North-America", false, out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_reload_retains_the_previous_snapshot()
    {
        var (provider, _) = NewProvider();
        provider.SetSnapshotForTest(new Dictionary<(int, string, bool), PriceBaseline>
        {
            [(12345, "Europe", false)] = new(1_000, 50, DateTimeOffset.UnixEpoch),
        });

        await provider.ReloadAsync();

        provider.TryGet(12345, "Europe", false, out var kept).Should().BeTrue(
            "a reload that throws must not blank the working snapshot");
        kept!.MedianUnitPrice.Should().Be(1_000);
    }
}
