using System.Collections.Concurrent;
using Ffmt.Cli.Commands;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Quarantine;

/// <summary>The baseline job batches items so the (item, world) fan-out keeps its semaphore busy;
/// what must not change is that every pair in a configured region is read exactly once.</summary>
public sealed class BaselineFanOutTests
{
    private static readonly World[] EuropeWorlds =
    [
        new(21, "Ravana", "Chaos", "Europe"),
        new(22, "Bismarck", "Chaos", "Europe"),
        new(80, "Alpha", "Light", "Europe"),
    ];

    private static readonly World OutOfRegion = new(40, "Gilgamesh", "Aether", "North-America");

    private sealed class RecordingSaleStore : FakeSaleStore
    {
        public ConcurrentBag<(int ItemId, int WorldId)> Reads { get; } = [];

        public override Task<IReadOnlyList<PricePoint>> GetPricePointsSinceAsync(
            int itemId, int worldId, DateTimeOffset since, CancellationToken ct = default)
        {
            Reads.Add((itemId, worldId));
            return Task.FromResult<IReadOnlyList<PricePoint>>([]);
        }
    }

    private static UpdateBaselinesCommand NewCommand(ISaleStore saleStore, IReadOnlyList<int> itemIds)
    {
        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<World>>([.. EuropeWorlds, OutOfRegion]));

        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetMarketableIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(itemIds));

        var worldStructure = new WorldStructureService(
            worldStore,
            itemStore,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()));

        return new UpdateBaselinesCommand(
            saleStore,
            Substitute.For<IScyllaSession>(),
            worldStore,
            worldStructure,
            Options.Create(new UniversalisOptions { RegionsToUse = ["europe"] }),
            Options.Create(new QuarantineOptions()),
            NullLogger<UpdateBaselinesCommand>.Instance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(140)]
    public async Task Reads_every_item_world_pair_in_the_region_exactly_once(int itemCount)
    {
        var itemIds = Enumerable.Range(1, itemCount).ToList();
        var saleStore = new RecordingSaleStore();

        await NewCommand(saleStore, itemIds).RunAsync(dryRun: true, CancellationToken.None);

        var expected = itemIds.SelectMany(id => EuropeWorlds.Select(w => (ItemId: id, WorldId: w.Id)));
        saleStore.Reads.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Never_reads_a_world_outside_the_configured_region()
    {
        var saleStore = new RecordingSaleStore();

        await NewCommand(saleStore, [1, 2, 3]).RunAsync(dryRun: true, CancellationToken.None);

        saleStore.Reads.Select(r => r.WorldId).Should().NotContain(OutOfRegion.Id);
    }
}
