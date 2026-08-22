using System.Collections.Concurrent;
using Ffmt.Core.External;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.External;

public sealed class MarketBoardReaderTests
{
    private static readonly World Odin = new(66, "Odin", "Light", "Europe");
    private static readonly World Shiva = new(67, "Shiva", "Light", "Europe");
    private static readonly World Omega = new(39, "Omega", "Chaos", "Europe");
    private static readonly World Gilgamesh = new(1, "Gilgamesh", "Aether", "North-America");

    private static MarketBoardReader Reader(FakeUniversalis universalis, params World[] worlds)
    {
        var structure = TestWorlds.Structure(worlds);
        return new MarketBoardReader(
            universalis, structure, new LocationResolver(structure), NullLogger<MarketBoardReader>.Instance);
    }

    [Fact]
    public async Task World_QueriesThatWorldOnly()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin, Shiva, Omega);

        await reader.GetAsync("Odin", [1, 2], CancellationToken.None);

        universalis.Calls.Select(c => c.Location).Should().Equal("Odin");
    }

    [Fact]
    public async Task Datacenter_FansOutToEveryWorldOnIt()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin, Shiva, Omega);

        await reader.GetAsync("Light", [1], CancellationToken.None);

        universalis.Calls.Select(c => c.Location).Should().BeEquivalentTo(["Odin", "Shiva"]);
    }

    [Fact]
    public async Task Region_FansOutToEveryWorldInIt()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin, Shiva, Omega, Gilgamesh);

        await reader.GetAsync("Europe", [1], CancellationToken.None);

        universalis.Calls.Select(c => c.Location).Should().BeEquivalentTo(["Odin", "Shiva", "Omega"]);
    }

    [Fact]
    public async Task ChunksItemIdsSoUniversalisNeverSeesMoreThanFifty()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin);
        var ids = Enumerable.Range(1, 123).ToList();

        await reader.GetAsync("Odin", ids, CancellationToken.None);

        universalis.Calls.Should().HaveCount(3);
        universalis.Calls.Select(c => c.Ids.Count).Should().Equal(50, 50, 23);
        universalis.Calls.SelectMany(c => c.Ids).Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task MinPriceIsTheCheapestListingAcrossWorlds()
    {
        var universalis = new FakeUniversalis()
            .With("Odin", 1, minPrice: 500)
            .With("Shiva", 1, minPrice: 320);
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].MinPrice.Should().Be(320);
    }

    [Fact]
    public async Task MinPriceIgnoresWorldsWithNothingListed()
    {
        var universalis = new FakeUniversalis()
            .With("Odin", 1, minPrice: 0)
            .With("Shiva", 1, minPrice: 320);
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].MinPrice.Should().Be(320);
    }

    [Fact]
    public async Task MinPriceIsZeroWhenNoWorldHasAListing()
    {
        var universalis = new FakeUniversalis()
            .With("Odin", 1, minPrice: 0)
            .With("Shiva", 1, minPrice: 0);
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].MinPrice.Should().Be(0);
    }

    [Fact]
    public async Task VelocitiesSumAcrossWorlds()
    {
        var universalis = new FakeUniversalis()
            .With("Odin", 1, velocity: 43.57)
            .With("Shiva", 1, velocity: 21.00);
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].RegularSaleVelocity.Should().BeApproximately(64.57, 0.001);
    }

    [Fact]
    public async Task StackHistogramsMergeBucketwiseAcrossWorlds()
    {
        var universalis = new FakeUniversalis()
            .With("Odin", 1, histogram: new Dictionary<int, int> { [1] = 3, [99] = 4 })
            .With("Shiva", 1, histogram: new Dictionary<int, int> { [99] = 5, [50] = 2 });
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].StackSizeHistogram.Should().BeEquivalentTo(
            new Dictionary<int, int> { [1] = 3, [50] = 2, [99] = 9 });
    }

    [Fact]
    public async Task UnknownLocationIsPassedThroughSoUniversalisRejectsIt()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin);

        await reader.GetAsync("Atlantis", [1], CancellationToken.None);

        universalis.Calls.Select(c => c.Location).Should().Equal("Atlantis");
    }

    [Fact]
    public async Task EmptyItemListNeverCallsUniversalis()
    {
        var universalis = new FakeUniversalis();
        var reader = Reader(universalis, Odin);

        var result = await reader.GetAsync("Light", [], CancellationToken.None);

        result.Should().BeEmpty();
        universalis.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task OneFailingWorldDoesNotSinkTheWholeQuery()
    {
        var universalis = new FakeUniversalis()
            .With("Shiva", 1, minPrice: 320)
            .Failing("Odin");
        var reader = Reader(universalis, Odin, Shiva);

        var result = await reader.GetAsync("Light", [1], CancellationToken.None);

        result[1].MinPrice.Should().Be(320);
    }

    [Fact]
    public async Task EveryWorldFailingSurfacesTheError()
    {
        var universalis = new FakeUniversalis().Failing("Odin").Failing("Shiva");
        var reader = Reader(universalis, Odin, Shiva);

        var act = () => reader.GetAsync("Light", [1], CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeUniversalis : IUniversalisClient
    {
        private readonly Dictionary<(string Location, int Id), UniversalisMarketBoardListing> _data = [];
        private readonly HashSet<string> _failing = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<(string Location, IReadOnlyList<int> Ids)> _recorded = [];

        public IReadOnlyList<(string Location, IReadOnlyList<int> Ids)> Calls =>
            _recorded.OrderBy(c => c.Location, StringComparer.Ordinal).ThenBy(c => c.Ids[0]).ToList();

        public FakeUniversalis With(string location, int itemId, int minPrice = 0, double velocity = 0,
            IReadOnlyDictionary<int, int>? histogram = null)
        {
            _data[(location, itemId)] = new UniversalisMarketBoardListing(
                minPrice, velocity, histogram ?? new Dictionary<int, int>());
            return this;
        }

        public FakeUniversalis Failing(string location)
        {
            _failing.Add(location);
            return this;
        }

        public Task<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> GetMarketBoardDataAsync(
            string location, IReadOnlyList<int> itemIds, CancellationToken ct = default)
        {
            _recorded.Enqueue((location, itemIds));

            if (_failing.Contains(location))
            {
                throw new HttpRequestException($"{location} is down");
            }

            var result = new Dictionary<int, UniversalisMarketBoardListing>();
            foreach (var id in itemIds)
            {
                if (_data.TryGetValue((location, id), out var listing))
                {
                    result[id] = listing;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<int, UniversalisMarketBoardListing>>(result);
        }

        public Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<World>> GetAllWorldsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
