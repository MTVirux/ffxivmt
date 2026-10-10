using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ffmt.Api.Endpoints;
using Ffmt.Core.Configuration;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Endpoints;

/// <summary>Drives the mapped route delegates directly, like <see cref="GilfluxEndpointsTests"/>, so
/// no second WebApplicationFactory is needed.</summary>
public sealed class ItemNamesEndpointsTests : IDisposable
{
    private const string NamesJson = """{"1":"Gil","2":"Fire Shard"}""";

    private static readonly IReadOnlyDictionary<int, string> Names =
        new Dictionary<int, string> { [1] = "Gil", [2] = "Fire Shard" };

    private readonly ServiceProvider _services;
    private readonly WorldStructureService _worldStructure;
    private readonly RouteEndpoint _namesEndpoint;
    private readonly RouteEndpoint _configEndpoint;

    public ItemNamesEndpointsTests()
    {
        var options = Options.Create(new GilfluxOptions
        {
            TimeframesMs = new Dictionary<string, long> { ["1h"] = 3_600_000, ["3h"] = 10_800_000 },
        });
        _worldStructure = NewWorldStructure(Names, options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        services.AddSingleton(options);
        services.AddSingleton(_worldStructure);

        // The sibling /item routes only need their services to resolve for their delegates to build.
        services.AddSingleton(Substitute.For<IItemStore>());
        services.AddSingleton(Substitute.For<ISaleStore>());
        services.AddSingleton(Substitute.For<IElasticItemSearch>());
        services.AddSingleton<ItemSalesReader>();
        _services = services.BuildServiceProvider();

        var builder = new TestRouteBuilder(_services);
        builder.MapItemEndpoints();
        builder.MapConfigEndpoints();
        var endpoints = builder.DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();
        _namesEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/v1/item/names");
        _configEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/v1/config");
    }

    public void Dispose() => _services.Dispose();

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = services;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    private static WorldStructureService NewWorldStructure(
        IReadOnlyDictionary<int, string> names, IOptions<GilfluxOptions>? options = null)
    {
        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetAllNamesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(names));

        return new WorldStructureService(
            Substitute.For<IWorldStore>(),
            itemStore,
            new MemoryCache(new MemoryCacheOptions()),
            options ?? Options.Create(new GilfluxOptions()));
    }

    private async Task<(int StatusCode, string Body, string CacheControl)> InvokeAsync(Endpoint endpoint, string query)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = body;

        await endpoint.RequestDelegate!(context);

        return (
            context.Response.StatusCode,
            Encoding.UTF8.GetString(body.ToArray()),
            context.Response.Headers.CacheControl.ToString());
    }

    private async Task<string> CurrentVersionAsync() =>
        (await _worldStructure.GetVersionedItemNamesAsync()).Version;

    [Fact]
    public async Task Version_is_the_first_16_hex_chars_of_a_sha256_over_the_names_sorted_by_id()
    {
        var unsorted = new Dictionary<int, string> { [2] = "Fire Shard", [1] = "Gil" };
        var expected = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes("1\tGil\n2\tFire Shard\n")))[..16];

        var versioned = await NewWorldStructure(unsorted).GetVersionedItemNamesAsync();

        versioned.Version.Should().Be(expected).And.MatchRegex("^[0-9a-f]{16}$");
        versioned.Names.Should().BeEquivalentTo(unsorted);
    }

    [Fact]
    public async Task Version_is_the_same_for_the_same_names_in_any_order()
    {
        var reordered = new Dictionary<int, string> { [2] = "Fire Shard", [1] = "Gil" };

        var version = (await NewWorldStructure(reordered).GetVersionedItemNamesAsync()).Version;

        version.Should().Be(await CurrentVersionAsync());
    }

    [Fact]
    public async Task Version_changes_when_a_name_changes()
    {
        var renamed = new Dictionary<int, string> { [1] = "Gil", [2] = "Fire Crystal" };

        var version = (await NewWorldStructure(renamed).GetVersionedItemNamesAsync()).Version;

        version.Should().NotBe(await CurrentVersionAsync());
    }

    [Fact]
    public async Task Names_requested_with_the_current_version_are_cacheable_for_a_year()
    {
        var (status, body, cacheControl) = await InvokeAsync(_namesEndpoint, $"?v={await CurrentVersionAsync()}");

        status.Should().Be(200);
        body.Should().Be($$"""{"status":true,"message":"Item names retrieved successfully","data":{{NamesJson}}}""");
        cacheControl.Should().Be("public, max-age=31536000, immutable");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?v=")]
    [InlineData("?v=0000000000000000")]
    public async Task Names_requested_without_the_current_version_are_not_cached(string query)
    {
        var (status, body, cacheControl) = await InvokeAsync(_namesEndpoint, query);

        status.Should().Be(200);
        body.Should().Be($$"""{"status":true,"message":"Item names retrieved successfully","data":{{NamesJson}}}""");
        cacheControl.Should().Be("no-store");
    }

    [Fact]
    public async Task Config_carries_the_item_names_version()
    {
        var version = await CurrentVersionAsync();

        var (status, body, _) = await InvokeAsync(_configEndpoint, "");

        status.Should().Be(200);
        body.Should().Be(
            $$$"""{"status":true,"data":{"gilflux_timeframes":["1h","3h"],"item_names_version":"{{{version}}}"}}""");
    }
}
