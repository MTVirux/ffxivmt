using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Gilflux;

public sealed class GilfluxRankingReaderDecayTests
{
    private const int SprigganId = 85;

    private static GilfluxRankingReader NewReader()
    {
        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<World>>([new World(SprigganId, "Spriggan", "Chaos", "Europe")]));

        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetAllNamesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [5057] = "Spriggan Ore" }));

        return new GilfluxRankingReader(
            Substitute.For<IGilfluxRankingStore>(),
            worldStore,
            itemStore,
            new LocationResolver(worldStore),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()));
    }

    private static GilfluxRanking Row(DateTimeOffset updatedAt) =>
        new(5057, SprigganId,
            new Dictionary<string, long> { ["1h"] = 100, ["3h"] = 100, ["6h"] = 100, ["12h"] = 100, ["1d"] = 100, ["3d"] = 100, ["7d"] = 100 },
            updatedAt.ToUnixTimeMilliseconds(),
            updatedAt.ToUnixTimeMilliseconds());

    [Fact]
    public async Task EnrichAsync_DropsRowsWhoseWidestTimeframeHasLapsed()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddDays(-66))]);

        enriched.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_ZeroesLapsedTimeframesOnARowStillInsideTheWindow()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddHours(-4))]);

        var row = enriched.Should().ContainSingle().Subject;
        row.Rankings["1h"].Should().Be(0);
        row.Rankings["3h"].Should().Be(0);
        row.Rankings["6h"].Should().Be(100);
        row.Rankings["7d"].Should().Be(100);
    }

    [Fact]
    public async Task EnrichAsync_KeepsAFreshRowIntact()
    {
        var reader = NewReader();

        var enriched = await reader.EnrichAsync([Row(DateTimeOffset.UtcNow.AddMinutes(-1))]);

        var row = enriched.Should().ContainSingle().Subject;
        row.ItemName.Should().Be("Spriggan Ore");
        row.WorldName.Should().Be("Spriggan");
        row.Rankings["1h"].Should().Be(100);
    }
}
