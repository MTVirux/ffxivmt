using Ffmt.Core.External;
using Ffmt.Core.GcSeals;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Ffmt.Tests.GcSeals;

public sealed class GcSealsCatalogueServiceTests
{
    private const int Sword = 1670;
    private const int Tool = 2339;
    private const int Unlisted = 3000;
    private const int Ingot = 5057;
    private const int Ore = 5111;
    private const int Shard = 2;

    private readonly IXivapiClient _xivapi = Substitute.For<IXivapiClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly GcSealsCatalogueService _service;

    public GcSealsCatalogueServiceTests()
    {
        _xivapi.GetEquipmentCandidatesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<XivapiEquipmentCandidate>>(
        [
            new(Sword, 2, 55, 1, 2),
            new(Sword, 2, 55, 1, 2),
            new(Tool, 2, 55, 2, 13),
            new(Unlisted, 2, 55, 1, 2),
        ]));
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<XivapiRecipe>>(
        [
            new(Sword, 1, [new(Ingot, 2), new(Shard, 3)]),
            new(Tool, 1, [new(Ingot, 1)]),
            new(Unlisted, 1, [new(Ore, 1)]),
            new(Ingot, 1, [new(Ore, 3), new(Shard, 1)]),
            new(Ingot, 1, [new(Ore, 3), new(Shard, 1)]),
            new(9999, 1, [new(Ore, 1)]),
        ]));
        _xivapi.GetExpertDeliverySealsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int> { [55] = 297 }));

        var items = TestWorlds.MarketableItems(Sword, Tool, Ingot, Ore, Shard);
        items.GetAllNamesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyDictionary<int, string>>(
            new Dictionary<int, string>
            {
                [Sword] = "Aeolian Scimitar",
                [Ingot] = "Iron Ingot",
                [Ore] = "Iron Ore",
                [Shard] = "Fire Shard",
            }));

        _service = new GcSealsCatalogueService(
            _xivapi, TestWorlds.Structure(TestWorlds.Store(), items), _cache, NullLogger<GcSealsCatalogueService>.Instance);
    }

    [Fact]
    public async Task Lists_eligible_marketable_craftable_items_once()
    {
        var catalogue = await _service.GetAsync();

        catalogue.Items.Should().Equal(new GcSealsItem(Sword, "Aeolian Scimitar", 55, 297));
    }

    [Fact]
    public async Task Includes_every_recipe_in_the_tree_and_nothing_else()
    {
        var catalogue = await _service.GetAsync();

        catalogue.Recipes.Select(r => r.ItemId).Should().Equal(Sword, Ingot, Ingot);
        catalogue.Recipes[0].Ingredients.Should().Equal(new GcSealsIngredient(Ingot, 2), new GcSealsIngredient(Shard, 3));
    }

    [Fact]
    public async Task Names_cover_every_referenced_id_in_id_order()
    {
        var catalogue = await _service.GetAsync();

        catalogue.Names.Keys.Should().Equal(Shard, Sword, Ingot, Ore);
        catalogue.Names[Ore].Should().Be("Iron Ore");
    }

    [Fact]
    public async Task Items_without_a_seal_value_are_dropped()
    {
        _xivapi.GetExpertDeliverySealsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>()));

        (await _service.GetAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Caches_the_catalogue()
    {
        await _service.GetAsync();
        await _service.GetAsync();

        await _xivapi.Received(1).GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Serves_the_last_good_catalogue_when_a_rebuild_fails()
    {
        var first = await _service.GetAsync();
        _cache.Remove(GcSealsCatalogueService.CacheKey);
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        var second = await _service.GetAsync();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task Throws_when_the_first_build_fails()
    {
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        var act = () => _service.GetAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
