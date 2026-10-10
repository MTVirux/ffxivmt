using Ffmt.Cli.Commands;
using Ffmt.Core.Models;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.Stages;

public sealed class BackfillMannequinCommandTests
{
    private static readonly Sale Mannequin =
        new(2, 21, "Alisaie", true, true, 1, 100, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    private sealed class OneRangeSaleStore : FakeSaleStore
    {
        private int _calls;
        public int Calls => _calls;

        public override Task<IReadOnlyList<Sale>> GetMannequinInTokenRangeAsync(long start, long end, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            IReadOnlyList<Sale> rows = start == long.MinValue ? [Mannequin] : [];
            return Task.FromResult(rows);
        }
    }

    private static readonly Sale LastRangeMannequin = Mannequin with { ItemId = 3 };

    // The first range fails `failures` times before returning Mannequin; the last range always returns LastRangeMannequin.
    private sealed class FlakyFirstRangeSaleStore(int failures) : FakeSaleStore
    {
        private int _firstRangeCalls;

        public override Task<IReadOnlyList<Sale>> GetMannequinInTokenRangeAsync(long start, long end, CancellationToken ct = default)
        {
            if (start == long.MinValue && Interlocked.Increment(ref _firstRangeCalls) <= failures)
            {
                return Task.FromException<IReadOnlyList<Sale>>(new TimeoutException("range timed out"));
            }

            IReadOnlyList<Sale> rows = start == long.MinValue ? [Mannequin] : end == long.MaxValue ? [LastRangeMannequin] : [];
            return Task.FromResult(rows);
        }
    }

    private static BackfillMannequinCommand NewCommand(FakeSaleStore sales, FakeMannequinSaleStore mannequin) =>
        new(sales, mannequin, NullLogger<BackfillMannequinCommand>.Instance) { RetryDelay = TimeSpan.Zero };

    [Fact]
    public async Task Dry_run_scans_every_range_and_writes_nothing()
    {
        var sales = new OneRangeSaleStore();
        var mannequin = new FakeMannequinSaleStore();

        await new BackfillMannequinCommand(sales, mannequin, NullLogger<BackfillMannequinCommand>.Instance)
            .RunAsync(dryRun: true, CancellationToken.None);

        sales.Calls.Should().Be(BackfillMannequinCommand.RangeCount);
        mannequin.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Real_run_copies_mannequin_sales()
    {
        var mannequin = new FakeMannequinSaleStore();

        await new BackfillMannequinCommand(new OneRangeSaleStore(), mannequin, NullLogger<BackfillMannequinCommand>.Instance)
            .RunAsync(dryRun: false, CancellationToken.None);

        mannequin.Sales.Should().Equal(Mannequin);
    }

    [Fact]
    public async Task A_range_that_fails_twice_is_retried_and_copied()
    {
        var mannequin = new FakeMannequinSaleStore();

        await NewCommand(new FlakyFirstRangeSaleStore(failures: 2), mannequin)
            .RunAsync(dryRun: false, CancellationToken.None);

        mannequin.Sales.Should().BeEquivalentTo([Mannequin, LastRangeMannequin]);
    }

    [Fact]
    public async Task A_range_that_keeps_failing_does_not_stop_the_others_but_fails_the_run()
    {
        var mannequin = new FakeMannequinSaleStore();

        await NewCommand(new FlakyFirstRangeSaleStore(failures: int.MaxValue), mannequin)
            .Invoking(c => c.RunAsync(dryRun: false, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        mannequin.Sales.Should().Equal(LastRangeMannequin);
    }
}
