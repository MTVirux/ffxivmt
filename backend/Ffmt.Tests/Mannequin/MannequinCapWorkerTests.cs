using Ffmt.Core.Configuration;
using Ffmt.Core.Mannequin;
using Ffmt.Core.Models;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ffmt.Tests.Mannequin;

public sealed class MannequinCapWorkerTests
{
    private static readonly World Spriggan = new(85, "Spriggan", "Chaos", "Europe");
    private static readonly World Twintania = new(86, "Twintania", "Light", "Europe");

    private static Sale At(int worldId, int day) =>
        new(1, worldId, "B", false, true, 1, 100, new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Sums_every_world_and_drops_the_oldest_day_for_all_of_them()
    {
        var store = new FakeMannequinSaleStore();
        await store.AddAsync([At(85, 1), At(86, 1), At(85, 2), At(86, 3)]);
        var worker = new MannequinCapWorker(
            store,
            TestWorlds.Structure(Spriggan, Twintania),
            Options.Create(new MannequinOptions { MaxSizeMb = 2, EstimatedBytesPerRow = 1024 * 1024 }),
            NullLogger<MannequinCapWorker>.Instance);

        var dropped = await worker.RunOnceAsync(CancellationToken.None);

        dropped.Should().Equal(new DateOnly(2026, 10, 1));
        store.Deletes.Should().ContainSingle();
        store.Deletes[0].WorldIds.Should().BeEquivalentTo([85, 86]);
        store.Sales.Should().HaveCount(2);
    }

    [Fact]
    public async Task An_empty_table_does_nothing()
    {
        var store = new FakeMannequinSaleStore();
        var worker = new MannequinCapWorker(
            store, TestWorlds.Structure(Spriggan), Options.Create(new MannequinOptions()),
            NullLogger<MannequinCapWorker>.Instance);

        (await worker.RunOnceAsync(CancellationToken.None)).Should().BeEmpty();
        store.Deletes.Should().BeEmpty();
    }
}
