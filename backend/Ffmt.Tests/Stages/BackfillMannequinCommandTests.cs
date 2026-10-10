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
}
