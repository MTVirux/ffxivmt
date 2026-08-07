using Ffmt.Cli.Commands;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ffmt.Tests.Quarantine;

public sealed class QuarantineScrubTests
{
    private const int WorldId = 21;
    private const int ItemId = 12345;

    private static readonly Sale Normal = new(ItemId, WorldId, "Alisaie", false, false, 1, 500, DateTimeOffset.UnixEpoch);
    private static readonly Sale AlsoNormal = new(ItemId, WorldId, "Krile", false, false, 2, 900, DateTimeOffset.UnixEpoch);
    private static readonly Sale Anomalous = new(ItemId, WorldId, "Alphinaud", false, false, 1, 999_999_999, DateTimeOffset.UnixEpoch);

    /// <summary>Serves the seeded batch once and nothing afterwards, so the assertions count a
    /// single pass rather than one per day of the window the scrub walks.</summary>
    private sealed class RecordingSaleStore(IReadOnlyList<Sale> onlyPage) : FakeSaleStore
    {
        private bool _served;

        public List<IReadOnlyList<Sale>> Deleted { get; } = [];
        public List<IReadOnlyList<Sale>> Backfilled { get; } = [];

        public override Task<IReadOnlyList<Sale>> GetByItemAndWorldInRangeAsync(int itemId, int worldId, DateOnly date, CancellationToken ct = default)
        {
            if (_served)
            {
                return Task.FromResult<IReadOnlyList<Sale>>([]);
            }

            _served = true;
            return Task.FromResult(onlyPage);
        }

        public override Task DeleteExactAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
        {
            Deleted.Add(sales);
            return Task.CompletedTask;
        }

        public override Task BackfillTotalPriceAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
        {
            Backfilled.Add(sales);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDirtyPairQueue : IDirtyPairQueue
    {
        public List<(int WorldId, int ItemId)> Enqueued { get; } = [];

        public Task EnqueueManyAsync(IReadOnlyCollection<(int WorldId, int ItemId)> pairs, CancellationToken ct = default)
        {
            Enqueued.AddRange(pairs);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DirtyPairClaim>> ClaimBatchAsync(int batchSize, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DirtyPairClaim>>([]);
        public Task RemoveAsync(IReadOnlyCollection<DirtyPairClaim> claims, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static QuarantineScrubCommand NewCommand(
        ISaleStore saleStore, IQuarantineStore quarantineStore, IDirtyPairQueue dirtyPairs)
    {
        var worldStore = TestWorlds.Store(new World(WorldId, "Ravana", "Chaos", "Europe"));
        var worldStructure = TestWorlds.Structure(worldStore, TestWorlds.MarketableItems(ItemId));

        return new QuarantineScrubCommand(
            saleStore,
            quarantineStore,
            new StubAnomalyFilter(),
            new StubBaselines(),
            worldStore,
            worldStructure,
            dirtyPairs,
            Options.Create(new GilfluxOptions()),
            Options.Create(new QuarantineOptions()),
            Options.Create(new ArchiveOptions()),
            NullLogger<QuarantineScrubCommand>.Instance);
    }

    [Fact]
    public async Task Armed_run_deletes_exactly_the_flagged_sales_and_nothing_else()
    {
        var sales = new RecordingSaleStore([Normal, Anomalous, AlsoNormal]);
        var command = NewCommand(sales, new CapturingQuarantineStore(), new RecordingDirtyPairQueue());

        await command.RunAsync(dryRun: false, CancellationToken.None);

        sales.Deleted.Should().ContainSingle();
        sales.Deleted[0].Should().Equal(Anomalous);
        sales.Deleted.SelectMany(batch => batch).Should()
            .NotContain(Normal, "a scrub that takes legitimate sales with it is unrecoverable")
            .And.NotContain(AlsoNormal);
        sales.Backfilled.Should().ContainSingle();
        sales.Backfilled[0].Should().Equal(Normal, AlsoNormal);
    }

    [Fact]
    public async Task Armed_run_quarantines_exactly_what_it_deleted()
    {
        var sales = new RecordingSaleStore([Normal, Anomalous]);
        var quarantine = new CapturingQuarantineStore();
        var command = NewCommand(sales, quarantine, new RecordingDirtyPairQueue());

        await command.RunAsync(dryRun: false, CancellationToken.None);

        quarantine.Written.Should().ContainSingle();
        quarantine.Written[0].Sale.Should().Be(Anomalous);
        quarantine.Written[0].Reason.Should().Be(QuarantineReasons.UnitPriceDeviation);
        quarantine.Written.Select(q => q.Sale).Should().Equal(sales.Deleted.SelectMany(batch => batch));
    }

    [Fact]
    public async Task Dry_run_writes_and_deletes_nothing()
    {
        var sales = new RecordingSaleStore([Normal, Anomalous]);
        var quarantine = new CapturingQuarantineStore();
        var dirtyPairs = new RecordingDirtyPairQueue();

        await NewCommand(sales, quarantine, dirtyPairs).RunAsync(dryRun: true, CancellationToken.None);

        sales.Deleted.Should().BeEmpty();
        sales.Backfilled.Should().BeEmpty();
        quarantine.Written.Should().BeEmpty();
        dirtyPairs.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Armed_run_enqueues_the_affected_pairs_for_a_gilflux_refresh()
    {
        var dirtyPairs = new RecordingDirtyPairQueue();
        var command = NewCommand(
            new RecordingSaleStore([Normal, Anomalous]), new CapturingQuarantineStore(), dirtyPairs);

        await command.RunAsync(dryRun: false, CancellationToken.None);

        dirtyPairs.Enqueued.Should().ContainSingle(
            "the deleted gil sits in gilflux_ranking until the pair is recomputed");
        dirtyPairs.Enqueued[0].Should().Be((WorldId, ItemId));
    }
}
