using System.Text;
using System.Text.Json;
using Ffmt.Api.Endpoints;
using Ffmt.Core.External;
using Ffmt.Core.GcSeals;
using Ffmt.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ffmt.Tests.Endpoints;

/// <summary>Drives the route delegate directly, like MannequinSalesEndpointsTests, to pin the body byte-for-byte.</summary>
public sealed class GcSealsEndpointsTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly RouteEndpoint _catalogue;

    public GcSealsEndpointsTests()
    {
        var xivapi = Substitute.For<IXivapiClient>();
        xivapi.GetEquipmentCandidatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<XivapiEquipmentCandidate>>([new(1670, 2, 55, 1, 2)]));
        xivapi.GetAllRecipesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<XivapiRecipe>>([new(1670, 1, [new(5111, 3)])]));
        xivapi.GetExpertDeliverySealsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int> { [55] = 297 }));

        var items = TestWorlds.MarketableItems(1670, 5111);
        items.GetAllNamesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyDictionary<int, string>>(
            new Dictionary<int, string> { [1670] = "Aeolian Scimitar", [5111] = "Iron Ore" }));

        var service = new GcSealsCatalogueService(
            xivapi, TestWorlds.Structure(TestWorlds.Store(), items),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<GcSealsCatalogueService>.Instance);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        services.AddSingleton(service);
        _services = services.BuildServiceProvider();

        var builder = new TestRouteBuilder(_services);
        builder.MapGcSealsEndpoints();
        _catalogue = builder.DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/tools/gc_seals/catalogue");
    }

    public void Dispose() => _services.Dispose();

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = services;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    [Fact]
    public async Task Serves_the_catalogue_in_snake_case()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = body;

        await _catalogue.RequestDelegate!(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        Encoding.UTF8.GetString(body.ToArray()).Should().Be(
            """{"status":true,"message":"GC seals catalogue","data":{"items":[{"id":1670,"name":"Aeolian Scimitar","item_level":55,"seals":297}],"recipes":[{"item_id":1670,"yield":1,"ingredients":[{"id":5111,"amount":3}]}],"names":{"1670":"Aeolian Scimitar","5111":"Iron Ore"}}}""");
    }
}
