using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Configuration;

namespace Ffmt.Tests.Configuration;

/// <summary>UniversalisOptions seeds no regions on purpose (see OptionsArrayBindingTests), so the
/// worlds endpoint filters its whole tree away and 404s unless appsettings supplies them.</summary>
public sealed class ApiRegionConfigurationTests
{
    private static UniversalisOptions ShippedOptions()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var options = new UniversalisOptions();
        config.GetSection(UniversalisOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Shipped_appsettings_supplies_the_regions_the_worlds_endpoint_filters_on()
    {
        ShippedOptions().RegionsToUse.Should().NotBeEmpty();
    }

    [Fact]
    public void Worlds_endpoint_still_has_worlds_after_filtering_to_those_regions()
    {
        var tree = WorldStructureService.Build([new World(21, "Ravana", "Chaos", "Europe")]);

        WorldStructureService.FilterToRegions(tree, ShippedOptions().RegionsToUse)
            .Should().NotBeEmpty();
    }
}
