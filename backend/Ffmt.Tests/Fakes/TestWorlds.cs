using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Fakes;

internal static class TestWorlds
{
    public static IWorldStore Store(params World[] worlds)
    {
        var store = Substitute.For<IWorldStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<World>>(worlds));
        return store;
    }

    public static IItemStore MarketableItems(params int[] itemIds)
    {
        var store = Substitute.For<IItemStore>();
        store.GetMarketableIdsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<int>>(itemIds));
        return store;
    }

    public static WorldStructureService Structure(params World[] worlds) =>
        Structure(Store(worlds), Substitute.For<IItemStore>());

    public static WorldStructureService Structure(IWorldStore worlds, IItemStore items) =>
        new(worlds, items, new MemoryCache(new MemoryCacheOptions()), Options.Create(new GilfluxOptions()));
}
