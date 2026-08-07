using Ffmt.Core.DI;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ffmt.Tests.Configuration;

/// <summary>ScyllaSaleStore owns the prepared-statement cache, so the read interface and the
/// filtered write path must share one instance of it rather than each building their own.</summary>
public sealed class SaleStoreRegistrationTests
{
    private static ServiceProvider NewProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFfmtCore(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ISaleStore_resolves_to_the_single_scylla_store_instance()
    {
        using var sp = NewProvider();

        sp.GetRequiredService<ISaleStore>().Should().BeSameAs(sp.GetRequiredService<ScyllaSaleStore>());
        sp.GetRequiredService<ScyllaSaleStore>().Should().BeSameAs(sp.GetRequiredService<ScyllaSaleStore>());
    }

    [Fact]
    public void ISaleWriter_resolves_to_the_quarantine_filter()
    {
        using var sp = NewProvider();

        sp.GetRequiredService<ISaleWriter>().Should().BeOfType<FilteringSaleStore>()
            .And.BeSameAs(sp.GetRequiredService<ISaleWriter>());
    }
}
