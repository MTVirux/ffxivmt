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

    private MannequinSalesReader NewReader(int maxDays = 365, World[]? worlds = null, int fullLoadMaxRows = 20_000)
    {
        var structure = TestWorlds.Structure(worlds ?? [Spriggan, Cerberus, Twintania]);
        return new MannequinSalesReader(
            _store,
            structure,
            new LocationResolver(structure),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new MannequinOptions { MaxDaysPerRequest = maxDays, FullLoadMaxRows = fullLoadMaxRows }));
    }

    private static DateTimeOffset StartOf(DateTimeOffset time) => new(time.UtcDateTime.Date, TimeSpan.Zero);

    private static DateTimeOffset Oct(int day, int hour = 12, int minute = 0) =>
        new(2026, 10, day, hour, minute, 0, TimeSpan.Zero);

    private static Sale At(int worldId, DateTimeOffset time, bool hq = false, int price = 100, int item = 1, string buyer = "B") =>
        new(item, worldId, buyer, hq, true, 1, price, time);

    private static MannequinFeedQuery Query(
        string? location, DateTimeOffset? before, int limit = 50, bool? hq = null, int minPrice = 0, string? buyer = null) =>
        new(location, before, limit, hq, minPrice, buyer);

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
    public async Task A_sparse_feed_fills_the_page_from_many_days_with_one_read_per_day()
    {
        var worlds = Enumerable.Range(1, 90).Select(i => new World(i, $"World{i}", $"Dc{i % 12}", "Europe")).ToArray();
        await _store.AddAsync([.. Enumerable.Range(0, 60).Select(d => At(d + 1, Oct(10).AddDays(-d)))]);

        var page = await NewReader(worlds: worlds).GetAsync(Query(null, Oct(11), limit: 50));

        page!.Sales.Should().HaveCount(50);
        page.Sales.Select(s => s.WorldId).Should().Equal(Enumerable.Range(1, 50));
        _store.Reads.Should().HaveCount(50, "a day is one read however many worlds there are");
        page.NextBefore.Should().Be(StartOf(Oct(10).AddDays(-49)));
    }

    [Fact]
    public async Task Rows_outside_the_location_do_not_count_toward_the_limit()
    {
        await _store.AddAsync([At(86, Oct(10)), At(85, Oct(9)), At(86, Oct(8)), At(80, Oct(7)), At(85, Oct(6))]);

        var page = await NewReader().GetAsync(Query("Chaos", Oct(11), limit: 2));

        page!.Sales.Select(s => (s.WorldId, s.SaleTime)).Should().Equal((85, Oct(9)), (80, Oct(7)));
        page.NextBefore.Should().Be(StartOf(Oct(7)));
    }

    [Fact]
    public async Task No_location_skips_worlds_missing_from_the_world_list()
    {
        await _store.AddAsync([At(85, Oct(10, 10)), At(999, Oct(10, 11))]);

        var page = await NewReader().GetAsync(Query(null, Oct(11)));

        page!.Sales.Select(s => s.WorldId).Should().Equal(85);
    }

    [Fact]
    public async Task A_filter_that_matches_nothing_stops_at_the_day_budget()
    {
        await _store.AddAsync([At(85, Oct(10)), At(80, Oct(9)), At(86, Oct(8)), At(85, Oct(7))]);

        var page = await NewReader(maxDays: 3).GetAsync(Query(null, Oct(11), hq: true));

        page!.Sales.Should().BeEmpty();
        _store.Reads.Should().Equal(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 8));
        page.NextBefore.Should().Be(StartOf(Oct(8)));
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

    [Theory]
    [InlineData(null, new[] { true, false })]
    [InlineData(true, new[] { true })]
    [InlineData(false, new[] { false })]
    public async Task Quality_filter_applies(bool? hq, bool[] expected)
    {
        await _store.AddAsync([At(85, Oct(10, 10), hq: false), At(85, Oct(10, 11), hq: true)]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11), hq: hq));

        page!.Sales.Select(s => s.Hq).Should().Equal(expected);
    }

    [Fact]
    public async Task First_page_cache_is_keyed_by_quality()
    {
        await _store.AddAsync([At(85, DateTimeOffset.UtcNow.AddHours(-1), hq: true)]);
        var reader = NewReader();

        await reader.GetAsync(Query("Spriggan", before: null, hq: true));
        var nq = await reader.GetAsync(Query("Spriggan", before: null, hq: false));

        nq!.Sales.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, new[] { "Aerith Gainsborough", "Tifa Lockhart", "AERITH" })]
    [InlineData("aerith", new[] { "Aerith Gainsborough", "AERITH" })]
    [InlineData("LOCK", new[] { "Tifa Lockhart" })]
    public async Task Buyer_filter_matches_part_of_a_name_ignoring_case(string? buyer, string[] expected)
    {
        await _store.AddAsync([
            At(85, Oct(10, 12), buyer: "Aerith Gainsborough"),
            At(85, Oct(10, 11), buyer: "Tifa Lockhart"),
            At(85, Oct(10, 10), buyer: "AERITH"),
        ]);

        var page = await NewReader().GetAsync(Query("Spriggan", Oct(11), buyer: buyer));

        page!.Sales.Select(s => s.BuyerName).Should().Equal(expected);
    }

    [Fact]
    public async Task First_page_cache_is_keyed_by_buyer()
    {
        var recent = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.AddAsync([At(85, recent, buyer: "Aerith"), At(85, recent.AddMinutes(-1), buyer: "Tifa")]);
        var reader = NewReader();

        await reader.GetAsync(Query("Spriggan", before: null));
        var filtered = await reader.GetAsync(Query("Spriggan", before: null, buyer: "tifa"));

        filtered!.Sales.Select(s => s.BuyerName).Should().Equal("Tifa");
    }

    [Fact]
    public async Task First_page_cache_ignores_buyer_case()
    {
        await _store.AddAsync([At(85, DateTimeOffset.UtcNow.AddHours(-1), buyer: "Aerith")]);
        var reader = NewReader();

        await reader.GetAsync(Query("Spriggan", before: null, buyer: "aerith"));
        var reads = _store.Reads.Count;
        await reader.GetAsync(Query("Spriggan", before: null, buyer: "AERITH"));

        _store.Reads.Should().HaveCount(reads);
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

        _store.Reads.Should().OnlyContain(d => d < new DateOnly(2026, 10, 10));
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

    [Fact]
    public async Task GetAllAsync_returns_every_row_newest_first()
    {
        await _store.AddAsync([
            At(85, Oct(8)),
            At(86, Oct(10), item: 2),
            At(80, Oct(10), item: 1, buyer: "b"),
            At(85, Oct(10), item: 1, buyer: "B"),
            At(80, Oct(9, 18)),
        ]);

        var sales = await NewReader().GetAllAsync();

        sales!.Select(s => (s.WorldId, s.SaleTime)).Should().Equal(
            (85, Oct(10)), (80, Oct(10)), (86, Oct(10)), (80, Oct(9, 18)), (85, Oct(8)));
    }

    [Fact]
    public async Task GetAllAsync_skips_worlds_missing_from_the_world_list()
    {
        await _store.AddAsync([At(85, Oct(10, 10)), At(999, Oct(10, 11))]);

        var sales = await NewReader().GetAllAsync();

        sales!.Select(s => s.WorldId).Should().Equal(85);
    }

    [Fact]
    public async Task GetAllAsync_returns_null_over_the_cap()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9)), At(85, Oct(8))]);

        var sales = await NewReader(fullLoadMaxRows: 2).GetAllAsync();

        sales.Should().BeNull();
        _store.AllReads.Should().Equal(3);
    }

    [Fact]
    public async Task GetAllAsync_returns_every_row_at_the_cap()
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9))]);

        var sales = await NewReader(fullLoadMaxRows: 2).GetAllAsync();

        sales.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(20_000)]
    [InlineData(1)]
    public async Task GetAllAsync_is_cached(int fullLoadMaxRows)
    {
        await _store.AddAsync([At(85, Oct(10)), At(85, Oct(9))]);
        var reader = NewReader(fullLoadMaxRows: fullLoadMaxRows);

        var first = await reader.GetAllAsync();
        var second = await reader.GetAllAsync();

        second.Should().BeEquivalentTo(first);
        _store.AllReads.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_shares_one_read_between_concurrent_callers()
    {
        await _store.AddAsync([At(85, Oct(10))]);
        var release = new TaskCompletionSource();
        _store.AllGate = release.Task;
        var reader = NewReader();

        var first = reader.GetAllAsync();
        var second = reader.GetAllAsync();
        release.SetResult();
        await Task.WhenAll(first, second);

        _store.AllReads.Should().HaveCount(1);
        (await second).Should().HaveCount(1);
    }
}
