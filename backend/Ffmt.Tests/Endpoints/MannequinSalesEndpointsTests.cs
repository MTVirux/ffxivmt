using System.Text;
using System.Text.Json;
using Ffmt.Api.Endpoints;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Mannequin;
using Ffmt.Core.Models;
using Ffmt.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ffmt.Tests.Endpoints;

/// <summary>Drives the route delegate directly, like GilfluxEndpointsTests, to pin the body byte-for-byte.</summary>
public sealed class MannequinSalesEndpointsTests : IDisposable
{
    private readonly FakeMannequinSaleStore _store = new();
    private readonly ServiceProvider _services;
    private readonly RouteEndpoint _endpoint;

    public MannequinSalesEndpointsTests()
    {
        var structure = TestWorlds.Structure(
            new World(85, "Spriggan", "Chaos", "Europe"),
            new World(86, "Twintania", "Light", "Europe"));
        var reader = new MannequinSalesReader(
            _store, structure, new LocationResolver(structure),
            new MemoryCache(new MemoryCacheOptions()), Options.Create(new MannequinOptions()));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        services.AddSingleton(reader);
        _services = services.BuildServiceProvider();

        var builder = new TestRouteBuilder(_services);
        builder.MapMannequinSalesEndpoints();
        _endpoint = builder.DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().Single();
    }

    public void Dispose() => _services.Dispose();

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = services;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    private async Task<(int StatusCode, string Body)> GetAsync(string query)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = body;

        await _endpoint.RequestDelegate!(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    private static readonly long Oct11Ms = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    [Theory]
    [InlineData("")]
    [InlineData("target_location=%20&")]
    public async Task Missing_location_returns_every_world(string location)
    {
        await _store.AddAsync([
            new Sale(5057, 85, "Alisaie", true, true, 1, 1000, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero)),
            new Sale(5057, 86, "Alphinaud", false, true, 1, 1000, new DateTimeOffset(2026, 10, 10, 13, 0, 0, TimeSpan.Zero)),
        ]);

        var (status, body) = await GetAsync($"?{location}before={Oct11Ms}");

        status.Should().Be(StatusCodes.Status200OK);
        JsonDocument.Parse(body).RootElement.GetProperty("data").EnumerateArray()
            .Select(s => s.GetProperty("world_id").GetInt32())
            .Should().Equal(86, 85);
    }

    [Fact]
    public async Task Hq_false_returns_only_nq_sales()
    {
        await _store.AddAsync([
            new Sale(5057, 85, "Alisaie", true, true, 1, 1000, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero)),
            new Sale(5057, 86, "Alphinaud", false, true, 1, 1000, new DateTimeOffset(2026, 10, 10, 13, 0, 0, TimeSpan.Zero)),
        ]);

        var (status, body) = await GetAsync($"?hq=false&before={Oct11Ms}");

        status.Should().Be(StatusCodes.Status200OK);
        var sales = JsonDocument.Parse(body).RootElement.GetProperty("data").EnumerateArray().ToList();
        sales.Select(s => s.GetProperty("world_id").GetInt32()).Should().Equal(86);
        sales.Select(s => s.GetProperty("hq").GetBoolean()).Should().Equal(false);
    }

    [Fact]
    public async Task Unknown_location_is_a_404()
    {
        var (status, _) = await GetAsync("?target_location=Nowhere");
        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public async Task Out_of_range_before_is_a_400(long before)
    {
        var (status, _) = await GetAsync($"?target_location=Spriggan&before={before}");
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Returns_the_feed_shape()
    {
        await _store.AddAsync([new Sale(5057, 85, "Alisaie", true, true, 2, 1000,
            new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero))]);

        var (status, body) = await GetAsync($"?target_location=Spriggan&before={Oct11Ms}");

        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Be(
            """{"status":true,"message":"Mannequin sales retrieved successfully","data":[{"item_id":5057,"world_id":85,"buyer_name":"Alisaie","sale_time":"2026-10-10T12:00:00+00:00","hq":true,"quantity":2,"unit_price":1000,"total_price":2000}],"next_before":null}""");
    }
}
