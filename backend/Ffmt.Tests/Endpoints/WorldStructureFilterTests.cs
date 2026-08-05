using Ffmt.Core.Models;
using Ffmt.Core.Worlds;

namespace Ffmt.Tests.Endpoints;

public sealed class WorldStructureFilterTests
{
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Tree() =>
        WorldStructureService.Build(
        [
            new World(21, "Ravana", "Chaos", "Europe"),
            new World(75, "Malboro", "Crystal", "North-America"),
            new World(90, "Ifrit", "Elemental", "Japan"),
        ]);

    [Fact]
    public void Drops_regions_we_do_not_ingest()
    {
        var filtered = WorldStructureService.FilterToRegions(Tree(), ["Europe", "North-America"]);

        filtered.Keys.Should().BeEquivalentTo(["Europe", "North-America"],
            "the API must not advertise worlds we hold no sales for");
    }

    [Fact]
    public void Keeps_the_datacenter_and_world_nesting_intact()
    {
        var filtered = WorldStructureService.FilterToRegions(Tree(), ["Europe"]);

        filtered["Europe"]["Chaos"]["21"].Should().Be("Ravana");
    }

    [Fact]
    public void Matches_region_names_case_insensitively()
    {
        var filtered = WorldStructureService.FilterToRegions(Tree(), ["europe"]);

        filtered.Should().ContainKey("Europe");
    }

    [Fact]
    public void An_empty_region_list_yields_an_empty_tree()
    {
        WorldStructureService.FilterToRegions(Tree(), []).Should().BeEmpty();
    }
}
