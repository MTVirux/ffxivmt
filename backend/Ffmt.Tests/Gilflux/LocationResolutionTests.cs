using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;

namespace Ffmt.Tests.Gilflux;

public sealed class LocationResolutionTests
{
    private static readonly World Gilgamesh = new(1, "Gilgamesh", "Aether", "North-America");
    private static readonly World Ramuh = new(4, "Ramuh", "Chaos", "Europe");

    [Fact]
    public void World_MatchesOnlyThatWorldId()
    {
        var resolution = new LocationResolution(LocationKind.World, "Gilgamesh", 1);

        resolution.Matches(Gilgamesh).Should().BeTrue();
        resolution.Matches(Ramuh).Should().BeFalse();
    }

    [Fact]
    public void Datacenter_MatchesEveryWorldOnIt()
    {
        var resolution = new LocationResolution(LocationKind.Datacenter, "aether", null);

        resolution.Matches(Gilgamesh).Should().BeTrue();
        resolution.Matches(Ramuh).Should().BeFalse();
    }

    [Fact]
    public void Region_MatchesEveryWorldInIt()
    {
        var resolution = new LocationResolution(LocationKind.Region, "EUROPE", null);

        resolution.Matches(Ramuh).Should().BeTrue();
        resolution.Matches(Gilgamesh).Should().BeFalse();
    }
}
