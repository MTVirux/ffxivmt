using Ffmt.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ffmt.Tests.Configuration;

/// <summary>
/// ConfigurationBinder.BindArray allocates <c>existing.Length + children.Count</c> and copies the
/// property's current value in first, so a non-empty array default is appended to rather than
/// replaced. In production this crawled Europe twice on every backfill pass and handed the Scylla
/// driver a contact point that does not resolve on the app VM.
/// </summary>
public sealed class OptionsArrayBindingTests
{
    private static T Bind<T>(string section, Dictionary<string, string?> values)
        where T : new()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var bound = new T();
        config.GetSection(section).Bind(bound);
        return bound;
    }

    [Fact]
    public void RegionsToImport_binds_exactly_what_configuration_supplies()
    {
        var options = Bind<UniversalisOptions>("Universalis", new()
        {
            ["Universalis:RegionsToImport:0"] = "Europe",
            ["Universalis:RegionsToImport:1"] = "North-America",
        });

        // A duplicated region is crawled twice per pass, doubling upstream requests and writes.
        options.RegionsToImport.Should().Equal(new[] { "Europe", "North-America" });
    }

    [Fact]
    public void RegionsToUse_binds_exactly_what_configuration_supplies()
    {
        var options = Bind<UniversalisOptions>("Universalis", new()
        {
            ["Universalis:RegionsToUse:0"] = "Europe",
            ["Universalis:RegionsToUse:1"] = "North-America",
        });

        // Duplicates here subscribe the websocket consumer to the same worlds twice.
        options.RegionsToUse.Should().Equal(new[] { "Europe", "North-America" });
    }

    [Fact]
    public void ContactPoints_bind_exactly_what_configuration_supplies()
    {
        var options = Bind<ScyllaOptions>("Scylla", new()
        {
            ["Scylla:ContactPoints:0"] = "10.0.0.20",
        });

        // The app VM overrides index 0 with the private IP; a retained default would add a host
        // that does not resolve there.
        options.ContactPoints.Should().Equal(new[] { "10.0.0.20" });
    }
}
