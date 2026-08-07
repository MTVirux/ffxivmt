using Ffmt.Cli.Commands;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.S3;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Archive;

/// <summary>Export state is keyed by (world, day). Reading it per item turns the nightly archive
/// into items x days redundant Scylla reads per world, so the call count is pinned here.</summary>
public sealed class ArchiveExportStateTests
{
    private const int LookbackDays = 3;

    private sealed class CountingArchiveStore(bool exported) : IArchiveStore
    {
        public int IsExportedCalls { get; private set; }

        public Task<bool> IsExportedAsync(int worldId, DateOnly date, CancellationToken ct = default)
        {
            IsExportedCalls++;
            return Task.FromResult(exported);
        }

        public Task MarkExportedAsync(int worldId, DateOnly date, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static ArchiveCommand NewCommand(IArchiveStore archiveStore, int itemCount)
    {
        var saleStore = Substitute.For<ISaleStore>();
        saleStore.GetByItemAndWorldInRangeAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Sale>>([]));

        var worldStore = Substitute.For<IWorldStore>();
        worldStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<World>>([new World(21, "Ravana", "Chaos", "Europe")]));

        var itemStore = Substitute.For<IItemStore>();
        itemStore.GetMarketableIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>(Enumerable.Range(1, itemCount).ToList()));

        var worldStructure = new WorldStructureService(
            worldStore,
            itemStore,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GilfluxOptions()));

        return new ArchiveCommand(
            saleStore,
            archiveStore,
            Substitute.For<IS3ArchiveUploader>(),
            worldStore,
            worldStructure,
            Options.Create(new GilfluxOptions()),
            Options.Create(new ArchiveOptions { LookbackDays = LookbackDays }),
            NullLogger<ArchiveCommand>.Instance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(500)]
    public async Task Export_state_is_read_once_per_day_not_once_per_item(int itemCount)
    {
        var archiveStore = new CountingArchiveStore(exported: true);

        await NewCommand(archiveStore, itemCount).RunAsync(dryRun: true, CancellationToken.None);

        // One window walk in the export pass, one in the corrections pass - never x itemCount.
        archiveStore.IsExportedCalls.Should().Be(2 * LookbackDays);
    }

    [Fact]
    public async Task Corrections_pass_reads_no_sales_when_nothing_was_exported()
    {
        var archiveStore = new CountingArchiveStore(exported: false);

        await NewCommand(archiveStore, itemCount: 50).RunAsync(dryRun: true, CancellationToken.None);

        archiveStore.IsExportedCalls.Should().Be(2 * LookbackDays);
    }
}
