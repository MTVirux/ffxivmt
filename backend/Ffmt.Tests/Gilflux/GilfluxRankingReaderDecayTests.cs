using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Gilflux;

public sealed class GilfluxRankingReaderDecayTests
{
    private const int SprigganId = 85;

    private static readonly IReadOnlyList<World> Worlds =
    [
        new World(SprigganId, "Spriggan", "Chaos", "Europe"),
        new World(86, "Twintania", "Light", "Europe"),
    ];

    private static (GilfluxRankingReader reader, IGilfluxRankingStore store, IWorldStore worldStore, IItemStore itemStore) NewParts()
    {
        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Worlds));

        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetAllNamesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [5057] = "Spriggan Ore" }));

        var worldStructure = new WorldStructureService(
            worldStore,
            itemStore,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()));

        var store = Substitute.For<IGilfluxRankingStore>();
        var reader = new GilfluxRankingReader(
            store,
            worldStructure,
            itemStore,
            new LocationResolver(worldStructure),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()),
            NullLogger<GilfluxRankingReader>.Instance);
        return (reader, store, worldStore, itemStore);
    }

    private static GilfluxRankingReader NewReader() => NewParts().reader;

    private static GilfluxRanking Row(DateTimeOffset updatedAt) => Row(SprigganId, updatedAt);

    private static GilfluxRanking Row(int worldId, DateTimeOffset updatedAt) =>
        new(5057, worldId,
            new Dictionary<string, long> { ["1h"] = 100, ["3h"] = 100, ["6h"] = 100, ["12h"] = 100, ["1d"] = 100, ["3d"] = 100, ["7d"] = 100 },
            updatedAt.ToUnixTimeMilliseconds(),
            updatedAt.ToUnixTimeMilliseconds());

    [Fact]
    public async Task EnrichAsync_DropsRowsWhoseWidestTimeframeHasLapsed()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddDays(-66))]);

        enriched.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_ZeroesLapsedTimeframesOnARowStillInsideTheWindow()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddHours(-4))]);

        var row = enriched.Should().ContainSingle().Subject;
        row.Rankings["1h"].Should().Be(0);
        row.Rankings["3h"].Should().Be(0);
        row.Rankings["6h"].Should().Be(100);
        row.Rankings["7d"].Should().Be(100);
    }

    [Fact]
    public async Task EnrichAsync_DropsRowsWhoseLastSaleHasAgedOutEvenWhenTheRowLooksFresh()
    {
        var reader = NewReader();
        var now = DateTimeOffset.UtcNow;
        var row = new GilfluxRanking(5057, SprigganId,
            new Dictionary<string, long> { ["1h"] = 100, ["7d"] = 100 },
            now.AddHours(-1).ToUnixTimeMilliseconds(),
            now.AddDays(-13).ToUnixTimeMilliseconds());

        var enriched = await reader.EnrichAsync([row]);

        enriched.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_KeepsAFreshRowIntact()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddMinutes(-1))]);

        var row = enriched.Should().ContainSingle().Subject;
        row.ItemName.Should().Be("Spriggan Ore");
        row.WorldName.Should().Be("Spriggan");
        row.Rankings["1h"].Should().Be(100);
    }

    [Fact]
    public async Task GetByLocationAsync_ReadsWorldsAndItemNamesOncePerCall()
    {
        var (reader, store, worldStore, itemStore) = NewParts();
        store.GetByWorldAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([]));

        await reader.GetByLocationAsync("Chaos", craftedOnly: false);

        await worldStore.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await itemStore.Received(1).GetAllNamesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByItemAndLocationAsync_UnknownLocationReturnsNull()
    {
        var (reader, _, _, _) = NewParts();

        var result = await reader.GetByItemAndLocationAsync(5057, "Nowhere");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByItemAndLocationAsync_WorldScopeReadsThatWorldOnly()
    {
        var (reader, store, _, _) = NewParts();
        store.GetByItemAndWorldAsync(5057, SprigganId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(DateTimeOffset.UtcNow.AddMinutes(-1))]));

        var result = await reader.GetByItemAndLocationAsync(5057, "Spriggan");

        result.Should().ContainSingle().Which.WorldName.Should().Be("Spriggan");
        await store.DidNotReceive().GetByItemAsync(5057, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByItemAndLocationAsync_DatacenterScopeDropsWorldsOutsideIt()
    {
        var (reader, store, _, _) = NewParts();
        var fresh = DateTimeOffset.UtcNow.AddMinutes(-1);
        store.GetByItemAsync(5057, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(SprigganId, fresh), Row(86, fresh)]));

        var result = await reader.GetByItemAndLocationAsync(5057, "Chaos");

        result.Should().ContainSingle().Which.WorldId.Should().Be(SprigganId);
    }

    [Fact]
    public async Task GetByItemAndLocationAsync_RegionScopeKeepsEveryMemberWorld()
    {
        var (reader, store, _, _) = NewParts();
        var fresh = DateTimeOffset.UtcNow.AddMinutes(-1);
        store.GetByItemAsync(5057, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(SprigganId, fresh), Row(86, fresh)]));

        var result = await reader.GetByItemAndLocationAsync(5057, "Europe");

        result!.Select(r => r.WorldId).Should().BeEquivalentTo(new int?[] { SprigganId, 86 });
    }
}
