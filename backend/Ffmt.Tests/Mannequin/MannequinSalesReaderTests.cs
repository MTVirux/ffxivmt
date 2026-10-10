using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Mannequin;
using Ffmt.Core.Models;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Tests.Mannequin;

public sealed class MannequinSalesReaderTests
{
    private static readonly World Spriggan = new(85, "Spriggan", "Chaos", "Europe");
    private static readonly World Cerberus = new(80, "Cerberus", "Chaos", "Europe");
    private static readonly World Twintania = new(86, "Twintania", "Light", "Europe");

    private readonly FakeMannequinSaleStore _store = new();

    private MannequinSalesReader NewReader(int maxDays = 7, int maxPartitionReads = 64)
    {
        var structure = TestWorlds.Structure(Spriggan, Cerberus, Twintania);
        return new MannequinSalesReader(
            _store,
            structure,
            new LocationResolver(structure),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new MannequinOptions
            {
                MaxDaysPerRequest = maxDays,
                MaxPartitionReadsPerRequest = maxPartitionReads,
            }));
    }

    private static DateTimeOffset Oct(int day, int hour = 12, int minute = 0) =>
        new(2026, 10, day, hour, minute, 0, TimeSpan.Zero);

    private static Sale At(int worldId, DateTimeOffset time, bool hq = false, int price = 100, int item = 1, string buyer = "B") =>
        new(item, worldId, buyer, hq, true, 1, price, time);

    private static MannequinFeedQuery Query(string? location, DateTimeOffset? before, int limit = 50, int minPrice = 0) =>
        new(location, before, limit, minPrice);

    [Fact]
    public async Task Unknown_location_returns_null()
    {
        (await NewReader().GetAsync(Query("Nowhere", Oct(11)))).Should().BeNull();
    }

    [Fact]
    public async Task World_scope_reads_only_that_world()
    {
        await _store.AddAsync([At(85, Oct(10)), At(86, Oct(10, 13))]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11)));

        page!.Sales.Select(s => s.WorldId).Should().Equal(85);
    }

    [Fact]
    public async Task Datacenter_scope_merges_its_worlds_newest_first()
    {
        await _store.AddAsync([At(85, Oct(10, 10)), At(80, Oct(10, 14)), At(86, Oct(10, 16))]);

        var page = await NewReader().GetAsync(Query("Chaos", Oct(11)));

        page!.Sales.Select(s => s.WorldId).Should().Equal(80, 85);
    }

    [Fact]
    public async Task No_location_merges_every_world_newest_first()
    {
        await _store.AddAsync([At(85, Oct(10, 10)), At(80, Oct(10, 14)), At(86, Oct(10, 16))]);

        var page = await NewReader().GetAsync(Query(null, Oct(11)));

        page!.Sales.Select(s => s.WorldId).Should().Equal(86, 80, 85);
    }

    [Fact]
    public async Task Walks_back_across_days_until_the_page_fills()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9, 18)), At(85, Oct(9, 6))]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11), limit: 2));

        page!.Sales.Select(s => s.SaleTime).Should().Equal(Oct(10), Oct(9, 18));
        page.NextBefore.Should().Be(Oct(9, 18));
    }

    [Fact]
    public async Task Stops_after_MaxDaysPerRequest_and_resumes_from_the_oldest_scanned_day()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9)), At(85, Oct(8))]);

        var page = await NewReader(maxDays: 2).GetAsync(Query("Spriggan", Oct(11)));

        page!.Sales.Should().HaveCount(2);
        page.NextBefore.Should().Be(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));

        var next = await NewReader(maxDays: 2).GetAsync(Query("Spriggan", page.NextBefore));
        next!.Sales.Select(s => s.SaleTime).Should().Equal(Oct(8));
        next.NextBefore.Should().BeNull();
    }

    [Fact]
    public async Task A_quiet_location_still_gets_a_cursor_while_older_days_remain()
    {
        await _store.AddAsync([At(86, Oct(10)), At(86, Oct(9)), At(85, Oct(8))]);

        var page = await NewReader(maxDays: 2).GetAsync(Query("Spriggan", Oct(11)));

        page!.Sales.Should().BeEmpty();
        page.NextBefore.Should().NotBeNull("Spriggan has a sale on an older indexed day");
    }

    [Fact]
    public async Task Wide_locations_scan_fewer_days_per_request()
    {
        await _store.AddAsync([At(85, Oct(10)), At(80, Oct(9)), At(85, Oct(8))]);

        var page = await NewReader(maxPartitionReads: 2).GetAsync(Query("Chaos", Oct(11)));

        _store.Reads.Should().HaveCount(2, "two worlds x one day fits a budget of two reads");
        page!.NextBefore.Should().Be(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task NextBefore_is_null_once_the_oldest_day_is_read()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9))]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11)));

        page!.Sales.Should().HaveCount(2);
        page.NextBefore.Should().BeNull();
    }

    [Fact]
    public async Task Min_price_filter_applies()
    {
        await _store.AddAsync([
            At(85, Oct(10, 10), hq: true, price: 500),
            At(85, Oct(10, 11), hq: false, price: 900),
            At(85, Oct(10, 12), hq: true, price: 50),
        ]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11), minPrice: 100));

        page!.Sales.Select(s => s.SaleTime).Should().Equal(Oct(10, 11), Oct(10, 10));
    }

    [Fact]
    public async Task A_cut_inside_equal_timestamps_keeps_the_whole_group()
    {
        await _store.AddAsync([
            At(85, Oct(10), item: 1), At(85, Oct(10), item: 2), At(85, Oct(10), item: 3),
            At(85, Oct(10, 9)),
        ]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11), limit: 2));

        page!.Sales.Should().HaveCount(3);
        page.NextBefore.Should().Be(Oct(10));

        var next = await NewReader().GetAsync(Query("Spriggan", page.NextBefore, limit: 2));
        next!.Sales.Select(s => s.SaleTime).Should().Equal(Oct(10, 9));
    }

    [Fact]
    public async Task A_midnight_cursor_starts_on_the_previous_day()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9))]);

        await NewReader().GetAsync(Query("Spriggan", new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero)));

        _store.Reads.Select(r => r.Day).Should().OnlyContain(d => d < new DateOnly(2026, 10, 10));
    }

    [Fact]
    public async Task The_first_page_is_cached()
    {
        await _store.AddAsync([At(85, DateTimeOffset.UtcNow.AddHours(-1))]);
        var reader = NewReader();

        await reader.GetAsync(Query("Spriggan", before: null));
        var reads = _store.Reads.Count;
        await reader.GetAsync(Query("Spriggan", before: null));

        reads.Should().BeGreaterThan(0);
        _store.Reads.Should().HaveCount(reads);
    }
}
