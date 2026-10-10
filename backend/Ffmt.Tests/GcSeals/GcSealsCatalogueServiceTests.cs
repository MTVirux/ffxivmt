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

    private static readonly IReadOnlyList<XivapiRecipe> Recipes =
    [
        new(Sword, 1, [new(Ingot, 2), new(Shard, 3)]),
        new(Tool, 1, [new(Ingot, 1)]),
        new(Unlisted, 1, [new(Ore, 1)]),
        new(Ingot, 1, [new(Ore, 3), new(Shard, 1)]),
        new(Ingot, 1, [new(Ore, 3), new(Shard, 1)]),
        new(9999, 1, [new(Ore, 1)]),
    ];

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
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Recipes));
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

    private TaskCompletionSource<IReadOnlyList<XivapiRecipe>> GateRecipes()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<XivapiRecipe>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        return gate;
    }

    private async Task WaitUntilCachedAsync()
    {
        for (var i = 0; i < 500 && !_cache.TryGetValue(GcSealsCatalogueService.CacheKey, out _); i++)
        {
            await Task.Delay(10);
        }
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

    [Fact]
    public async Task A_cancelled_caller_does_not_discard_the_build()
    {
        var gate = GateRecipes();
        using var cts = new CancellationTokenSource();

        var first = _service.GetAsync(cts.Token);
        await cts.CancelAsync();
        var second = _service.GetAsync();
        gate.SetResult(Recipes);

        var cancelled = () => first;
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
        (await second).Items.Should().Equal(new GcSealsItem(Sword, "Aeolian Scimitar", 55, 297));
        await _xivapi.Received(1).GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_callers_share_one_build()
    {
        var gate = GateRecipes();

        var first = _service.GetAsync();
        var second = _service.GetAsync();
        gate.SetResult(Recipes);

        (await second).Should().BeSameAs(await first);
        await _xivapi.Received(1).GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_expired_catalogue_is_served_stale_while_it_rebuilds()
    {
        var first = await _service.GetAsync();
        _cache.Remove(GcSealsCatalogueService.CacheKey);
        var gate = GateRecipes();

        var stale = _service.GetAsync();

        stale.IsCompletedSuccessfully.Should().BeTrue();
        (await stale).Should().BeSameAs(first);

        gate.SetResult(Recipes);
        await WaitUntilCachedAsync();

        var rebuilt = await _service.GetAsync();
        rebuilt.Should().NotBeSameAs(first).And.BeEquivalentTo(first);
        await _xivapi.Received(2).GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_rebuild_is_not_retried_inside_the_retry_window()
    {
        var first = await _service.GetAsync();
        _cache.Remove(GcSealsCatalogueService.CacheKey);
        _xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));
        await _service.GetAsync();
        await WaitUntilCachedAsync();
        _xivapi.ClearReceivedCalls();

        var third = await _service.GetAsync();

        third.Should().BeSameAs(first);
        await _xivapi.DidNotReceive().GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_callers_share_a_failed_first_build()
    {
        var gate = GateRecipes();

        var first = _service.GetAsync();
        var second = _service.GetAsync();
        gate.SetException(new HttpRequestException("down"));

        var firstAct = () => first;
        var secondAct = () => second;
        await firstAct.Should().ThrowAsync<HttpRequestException>();
        await secondAct.Should().ThrowAsync<HttpRequestException>();
        await _xivapi.Received(1).GetAllRecipesAsync(Arg.Any<CancellationToken>());
    }
}
