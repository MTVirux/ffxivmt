using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class ItemSalesReaderTests
{
    private static readonly IReadOnlyList<World> Worlds = new[]
    {
        new World(1, "Gilgamesh", "Aether", "North-America"),
        new World(2, "Faerie", "Aether", "North-America"),
        new World(3, "Behemoth", "Primal", "North-America"),
        new World(4, "Ramuh", "Chaos", "Europe"),
    };

    private static Sale SaleAt(int worldId, DateTimeOffset t) =>
        new(ItemId: 100, WorldId: worldId, BuyerName: "B", Hq: false, OnMannequin: false,
            Quantity: 1, UnitPrice: 10, SaleTime: t);

    private static (ItemSalesReader reader, ISaleStore sales) NewReader()
    {
        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Worlds);
        var sales = Substitute.For<ISaleStore>();
        var resolver = new LocationResolver(worldStore);
        return (new ItemSalesReader(sales, worldStore, resolver), sales);
    }

    [Fact]
    public async Task UnknownLocation_ReturnsNull()
    {
        var (reader, _) = NewReader();
        var result = await reader.GetByItemAndLocationAsync(100, "Nowhere", 50);
        result.Should().BeNull();
    }

    [Fact]
    public async Task World_DelegatesToSingleWorldRead()
    {
        var (reader, sales) = NewReader();
        var rows = new List<Sale> { SaleAt(1, DateTimeOffset.UnixEpoch) };
        sales.GetByItemAndWorldAsync(100, 1, 50, Arg.Any<CancellationToken>()).Returns(rows);

        var result = await reader.GetByItemAndLocationAsync(100, "Gilgamesh", 50);

        result.Should().BeEquivalentTo(rows);
        await sales.Received(1).GetByItemAndWorldAsync(100, 1, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Datacenter_MergesMemberWorldsSortedDescAndLimited()
    {
        var (reader, sales) = NewReader();
        var t0 = DateTimeOffset.UnixEpoch;
        sales.GetByItemAndWorldAsync(100, 1, 2, Arg.Any<CancellationToken>())
            .Returns(new List<Sale> { SaleAt(1, t0.AddMinutes(10)), SaleAt(1, t0.AddMinutes(1)) });
        sales.GetByItemAndWorldAsync(100, 2, 2, Arg.Any<CancellationToken>())
            .Returns(new List<Sale> { SaleAt(2, t0.AddMinutes(5)) });

        var result = await reader.GetByItemAndLocationAsync(100, "Aether", 2);

        result!.Select(s => s.SaleTime).Should().ContainInOrder(t0.AddMinutes(10), t0.AddMinutes(5));
        result.Should().HaveCount(2);
        await sales.DidNotReceive().GetByItemAndWorldAsync(100, 3, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Region_QueriesEveryWorldInRegionOnly()
    {
        var (reader, sales) = NewReader();
        var t0 = DateTimeOffset.UnixEpoch;
        sales.GetByItemAndWorldAsync(100, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => new List<Sale> { SaleAt((int)ci[1], t0.AddMinutes((int)ci[1])) });

        var result = await reader.GetByItemAndLocationAsync(100, "North-America", 50);

        result!.Select(s => s.WorldId).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        await sales.DidNotReceive().GetByItemAndWorldAsync(100, 4, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
