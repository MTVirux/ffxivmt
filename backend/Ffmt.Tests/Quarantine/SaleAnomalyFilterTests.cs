using System.Diagnostics.CodeAnalysis;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Quarantine;

public sealed class SaleAnomalyFilterTests
{
    private const int ItemId = 12345;
    private const int WorldId = 21;

    private sealed class StubBaselines : IPriceBaselineProvider
    {
        public Dictionary<(int, string, bool), PriceBaseline> Rows { get; } = new();
        public Task EnsureLoadedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public bool TryGet(int itemId, string region, bool hq, [MaybeNullWhen(false)] out PriceBaseline baseline) =>
            Rows.TryGetValue((itemId, region, hq), out baseline);
    }

    private static WorldStructureService NewWorlds()
    {
        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<World>>(
                [new World(WorldId, "Ravana", "Chaos", "Europe")]));

        return new WorldStructureService(
            worldStore,
            Substitute.For<IItemStore>(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()));
    }

    private static SaleAnomalyFilter NewFilter(StubBaselines baselines, QuarantineOptions? opts = null) =>
        new(baselines, NewWorlds(), Options.Create(opts ?? new QuarantineOptions()));

    private static Sale NewSale(int unitPrice, bool hq = false) =>
        new(ItemId, WorldId, "Alisaie", hq, false, 1, unitPrice, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Quarantines_a_sale_far_above_the_regional_median()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000_000, 50, DateTimeOffset.UnixEpoch);

        var result = await NewFilter(baselines).PartitionAsync([NewSale(999_999_999)]);

        result.Accepted.Should().BeEmpty();
        result.Quarantined.Should().ContainSingle();
        result.Quarantined[0].Reason.Should().Be(QuarantineReasons.UnitPriceDeviation);
        result.Quarantined[0].BaselineMedian.Should().Be(1_000_000);
    }

    [Fact]
    public async Task Accepts_a_sale_just_under_the_multiplier()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000_000, 50, DateTimeOffset.UnixEpoch);

        // default MedianMultiplier is 20 -> threshold 20,000,000
        var result = await NewFilter(baselines).PartitionAsync([NewSale(19_999_999)]);

        result.Accepted.Should().ContainSingle();
        result.Quarantined.Should().BeEmpty();
    }

    [Fact]
    public async Task Accepts_and_reports_no_baseline_when_the_slice_is_unknown()
    {
        var result = await NewFilter(new StubBaselines()).PartitionAsync([NewSale(999_999_999)]);

        result.Accepted.Should().ContainSingle("a missing baseline must fail open");
        result.Quarantined.Should().BeEmpty();
        result.NoBaseline.Should().ContainSingle();
    }

    [Fact]
    public async Task Accepts_and_reports_no_baseline_when_the_sample_count_is_too_thin()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000, 3, DateTimeOffset.UnixEpoch);

        var result = await NewFilter(baselines).PartitionAsync([NewSale(999_999_999)]);

        result.Accepted.Should().ContainSingle();
        result.NoBaseline.Should().ContainSingle("MinSampleCount defaults to 10 and 3 is below it");
    }

    [Fact]
    public async Task Never_quarantines_at_or_below_the_absolute_floor()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1, 50, DateTimeOffset.UnixEpoch);

        var result = await NewFilter(baselines).PartitionAsync([NewSale(100_000)]);

        result.Accepted.Should().ContainSingle(
            "a 1-gil median would otherwise flag every ordinary sale of a junk item");
        result.Quarantined.Should().BeEmpty();
    }

    [Fact]
    public async Task Hq_and_nq_resolve_to_separate_baselines()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000, 50, DateTimeOffset.UnixEpoch);
        baselines.Rows[(ItemId, "Europe", true)] = new PriceBaseline(10_000_000, 50, DateTimeOffset.UnixEpoch);

        var result = await NewFilter(baselines).PartitionAsync([NewSale(150_000_000, hq: true)]);

        result.Accepted.Should().ContainSingle(
            "hq gear legitimately sells for many times the nq price of the same item id");
    }

    [Fact]
    public async Task Disabled_bypasses_evaluation_entirely()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000, 50, DateTimeOffset.UnixEpoch);

        var result = await NewFilter(baselines, new QuarantineOptions { Enabled = false })
            .PartitionAsync([NewSale(999_999_999)]);

        result.Accepted.Should().ContainSingle();
        result.Quarantined.Should().BeEmpty();
        result.NoBaseline.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_world_fails_open()
    {
        var baselines = new StubBaselines();
        baselines.Rows[(ItemId, "Europe", false)] = new PriceBaseline(1_000, 50, DateTimeOffset.UnixEpoch);

        var stray = new Sale(ItemId, 9999, "Alisaie", false, false, 1, 999_999_999, DateTimeOffset.UnixEpoch);
        var result = await NewFilter(baselines).PartitionAsync([stray]);

        result.Accepted.Should().ContainSingle();
        result.NoBaseline.Should().ContainSingle();
    }
}
