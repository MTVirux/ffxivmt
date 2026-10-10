using Ffmt.Core.External;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Ffmt.Tests.Fakes;

/// <summary>The API host installs a process-global runtime metrics collector that only tolerates
/// one instance, so every test class that boots it shares this factory via <see cref="ApiCollection"/>.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public IPrometheusClient Prometheus { get; } = Substitute.For<IPrometheusClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Makes /api/v1/worlds throw an exception no handler maps, or cancel when the caller hung up.
        var worlds = Substitute.For<IWorldStore>();
        worlds.GetAllAsync(Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var ct = ci.Arg<CancellationToken>();
            return ct.IsCancellationRequested
                ? Task.FromCanceled<IReadOnlyList<World>>(ct)
                : Task.FromException<IReadOnlyList<World>>(new InvalidOperationException("boom"));
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(worlds);
            services.AddSingleton(Prometheus);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
