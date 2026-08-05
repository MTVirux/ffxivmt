using Ffmt.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ffmt.Tests.Configuration;

public sealed class QuarantineOptionsTests
{
    [Fact]
    public void Defaults_ship_in_shadow_mode_so_nothing_is_diverted_until_observed()
    {
        var opts = new QuarantineOptions();

        opts.Enabled.Should().BeTrue();
        opts.ShadowMode.Should().BeTrue("the feature ships observing, and is armed in a later deploy");
        opts.MedianMultiplier.Should().Be(20.0);
        opts.MinSampleCount.Should().Be(10);
        opts.MinAbsoluteUnitPrice.Should().Be(100_000);
        opts.BaselineWindowDays.Should().Be(7);
        opts.BaselineTtlDays.Should().Be(30);
        opts.BaselineRefreshMinutes.Should().Be(60);
        opts.BaselineComputeConcurrency.Should().Be(16);
    }

    [Fact]
    public void Binds_from_the_Quarantine_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quarantine:ShadowMode"] = "false",
                ["Quarantine:MedianMultiplier"] = "12.5",
                ["Quarantine:MinAbsoluteUnitPrice"] = "250000",
            })
            .Build();

        var opts = config.GetSection(QuarantineOptions.SectionName).Get<QuarantineOptions>()!;

        opts.ShadowMode.Should().BeFalse();
        opts.MedianMultiplier.Should().Be(12.5);
        opts.MinAbsoluteUnitPrice.Should().Be(250_000);
    }
}
