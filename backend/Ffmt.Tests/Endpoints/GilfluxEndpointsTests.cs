using System.Text;
using System.Text.Json;
using Ffmt.Api.Endpoints;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Endpoints;

/// <summary>Drives the mapped route delegates directly so response bodies are pinned byte-for-byte,
/// key order included. A bare route builder rather than a second WebApplicationFactory - the API
/// host installs a process-global runtime metrics collector that only tolerates one.</summary>
public sealed class GilfluxEndpointsTests : IDisposable
{
    private const int ItemId = 5057;
    private const int SprigganId = 85;
    private const int TwintaniaId = 86;

    private static readonly IReadOnlyList<World> Worlds =
    [
        new World(SprigganId, "Spriggan", "Chaos", "Europe"),
        new World(TwintaniaId, "Twintania", "Light", "Europe"),
    ];

    private readonly ServiceProvider _services;
    private readonly IGilfluxRankingStore _store = Substitute.For<IGilfluxRankingStore>();
    private readonly RouteEndpoint _listEndpoint;
    private readonly RouteEndpoint _itemEndpoint;

    public GilfluxEndpointsTests()
    {
        var options = Options.Create(new GilfluxOptions
        {
            TimeframesMs = new Dictionary<string, long> { ["1h"] = 3_600_000 },
        });

        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Worlds));

        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetAllNamesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, string>>(
                new Dictionary<int, string> { [ItemId] = "Spriggan Ore" }));
        itemStore.GetCraftableIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>([ItemId]));

        var worldStructure = new WorldStructureService(
            worldStore, itemStore, new MemoryCache(new MemoryCacheOptions()), options);
        var resolver = new LocationResolver(worldStructure);
        var reader = new GilfluxRankingReader(
            _store,
            worldStructure,
            itemStore,
            resolver,
            new MemoryCache(new MemoryCacheOptions()),
            options,
            NullLogger<GilfluxRankingReader>.Instance);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        services.AddSingleton(options);
        services.AddSingleton(_store);
        services.AddSingleton(resolver);
        services.AddSingleton(reader);
        _services = services.BuildServiceProvider();

        var builder = new TestRouteBuilder(_services);
        builder.MapGilfluxEndpoints();
        var endpoints = builder.DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();
        _itemEndpoint = endpoints.Single(e => e.RoutePattern.RawText!.Contains("{item_id", StringComparison.Ordinal));
        _listEndpoint = endpoints.Single(e => !e.RoutePattern.RawText!.Contains("{item_id", StringComparison.Ordinal));
    }

    public void Dispose() => _services.Dispose();

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = services;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    private async Task<(int StatusCode, string Body)> InvokeAsync(
        Endpoint endpoint, string query, (string Key, object Value)[]? routeValues = null)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString(query);
        foreach (var (key, value) in routeValues ?? [])
        {
            context.Request.RouteValues[key] = value;
        }
        context.Response.Body = body;

        await endpoint.RequestDelegate!(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    private Task<(int StatusCode, string Body)> GetItemAsync(string itemId, string query) =>
        InvokeAsync(_itemEndpoint, query, [("item_id", itemId)]);

    private Task<(int StatusCode, string Body)> GetListAsync(string query) =>
        InvokeAsync(_listEndpoint, query);

    private static long FreshMs => DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();

    private static GilfluxRanking Row(int? worldId, long stampMs) =>
        new(ItemId, worldId, new Dictionary<string, long> { ["1h"] = 100 }, stampMs, stampMs);

    private static string RowJson(int? worldId, string? worldName, string datacenter, string region, long stampMs) =>
        $$"""{"item_id":{{ItemId}},"item_name":"Spriggan Ore","world_id":{{worldId?.ToString() ?? "null"}},"world_name":{{(worldName is null ? "null" : $"\"{worldName}\"")}},"datacenter":"{{datacenter}}","region":"{{region}}","rankings":{"1h":100},"updated_at":{{stampMs}},"last_sale_time":{{stampMs}}}""";

    private static string Envelope(string message, string dataJson, string requestIdJson) =>
        $$"""{"status":true,"message":"{{message}}","data":{{dataJson}},"gilflux_timeframe_in_ms":{"1h":3600000},"request_id":{{requestIdJson}}}""";

    [Fact]
    public async Task Item_world_scope_returns_only_that_world()
    {
        var stamp = FreshMs;
        _store.GetByItemAndWorldAsync(ItemId, SprigganId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(SprigganId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?target_location=Spriggan&request_id=abc");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "\"abc\""));
        await _store.DidNotReceive().GetByItemAsync(ItemId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Item_datacenter_scope_drops_worlds_on_other_datacenters()
    {
        var stamp = FreshMs;
        _store.GetByItemAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>(
                [Row(SprigganId, stamp), Row(TwintaniaId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?target_location=chaos");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "null"));
    }

    [Fact]
    public async Task Item_region_scope_keeps_every_member_world_in_store_order()
    {
        var stamp = FreshMs;
        _store.GetByItemAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>(
                [Row(TwintaniaId, stamp), Row(SprigganId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?target_location=EUROPE");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(TwintaniaId, "Twintania", "Light", "Europe", stamp)},{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "null"));
    }

    [Fact]
    public async Task Item_scoped_read_drops_rows_with_no_world_and_rows_for_unknown_worlds()
    {
        var stamp = FreshMs;
        _store.GetByItemAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>(
                [Row(null, stamp), Row(999, stamp), Row(SprigganId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?target_location=Europe");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "null"));
    }

    [Fact]
    public async Task Item_scoped_read_drops_rows_whose_timeframes_have_all_lapsed()
    {
        var stamp = FreshMs;
        var lapsed = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds();
        _store.GetByItemAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>(
                [Row(TwintaniaId, lapsed), Row(SprigganId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?target_location=Europe");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "null"));
    }

    [Fact]
    public async Task Item_without_target_location_returns_every_row_unfiltered()
    {
        var stamp = FreshMs;
        _store.GetByItemAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>(
                [Row(null, stamp), Row(TwintaniaId, stamp)]));

        var (status, body) = await GetItemAsync("5057", "?request_id=r1");

        status.Should().Be(200);
        body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(null, null, string.Empty, string.Empty, stamp)},{RowJson(TwintaniaId, "Twintania", "Light", "Europe", stamp)}]",
            "\"r1\""));
    }

    [Fact]
    public async Task Item_unknown_location_returns_404_failure_envelope()
    {
        var (status, body) = await GetItemAsync("5057", "?target_location=Nowhere");

        status.Should().Be(404);
        body.Should().Be("""{"status":false,"message":"Unknown location 'Nowhere'"}""");
    }

    [Fact]
    public async Task Item_invalid_id_returns_400_failure_envelope()
    {
        var (status, body) = await GetItemAsync("0", "?target_location=Spriggan");

        status.Should().Be(400);
        body.Should().Be("""{"status":false,"message":"Invalid item_id"}""");
    }

    [Fact]
    public async Task List_without_target_location_returns_400_failure_envelope()
    {
        var (status, body) = await GetListAsync("");

        status.Should().Be(400);
        body.Should().Be("""{"status":false,"message":"No target location provided"}""");
    }

    [Fact]
    public async Task List_unknown_location_returns_404_failure_envelope()
    {
        var (status, body) = await GetListAsync("?target_location=Nowhere");

        status.Should().Be(404);
        body.Should().Be("""{"status":false,"message":"Unknown location 'Nowhere'"}""");
    }

    [Fact]
    public async Task List_success_returns_the_full_envelope_and_flips_message_on_a_cache_hit()
    {
        var stamp = FreshMs;
        _store.GetByWorldAsync(SprigganId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(SprigganId, stamp)]));

        var first = await GetListAsync("?target_location=Spriggan&request_id=r2");
        first.StatusCode.Should().Be(200);
        first.Body.Should().Be(Envelope(
            "Success",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "\"r2\""));

        var second = await GetListAsync("?target_location=Spriggan&request_id=r2");
        second.Body.Should().Be(Envelope(
            "Retrieved from cache",
            $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]",
            "\"r2\""));
    }

    [Fact]
    public async Task List_crafted_only_truthiness_matches_the_documented_parsing()
    {
        var stamp = FreshMs;
        _store.GetByWorldAsync(SprigganId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GilfluxRanking>>([Row(SprigganId, stamp)]));

        var craftedRows = $"[{RowJson(SprigganId, "Spriggan", "Chaos", "Europe", stamp)}]";

        var on = await GetListAsync("?target_location=Spriggan&crafted_only=yes");
        on.Body.Should().Be(Envelope("Success", craftedRows, "null"));

        var off = await GetListAsync("?target_location=Twintania&crafted_only=0");
        off.StatusCode.Should().Be(200);
    }
}
