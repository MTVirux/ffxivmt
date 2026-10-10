using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Fakes;

// Partitions like the Scylla table and its by-day view so reader and cap tests exercise real day boundaries.
// Days is the mannequin_sales_days index: removing from Sales directly simulates a scrub that leaves the
// index row behind.
internal sealed class FakeMannequinSaleStore : IMannequinSaleStore
{
    private readonly object _gate = new();

    public List<Sale> Sales { get; } = [];
    public SortedSet<DateOnly> Days { get; } = new();
    public List<DateOnly> Reads { get; } = [];
    public List<int> AllReads { get; } = [];
    public Task? AllGate { get; set; }
    public List<(IReadOnlyCollection<int> WorldIds, DateOnly Day)> Deletes { get; } = [];

    public Task AddAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var s in sales.Where(s => s.OnMannequin))
            {
                Sales.Add(s);
                Days.Add(MannequinCql.DayOf(s.SaleTime));
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Sale>> GetByDayAsync(DateOnly day, DateTimeOffset before, CancellationToken ct = default)
    {
        lock (_gate)
        {
            Reads.Add(day);
            IReadOnlyList<Sale> rows = Sales
                .Where(s => MannequinCql.DayOf(s.SaleTime) == day && s.SaleTime < before)
                .OrderByDescending(s => s.SaleTime)
                .ThenBy(s => s.WorldId)
                .ThenBy(s => s.ItemId)
                .ThenBy(s => s.BuyerName, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(rows);
        }
    }

    public async Task<IReadOnlyList<Sale>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        lock (_gate)
        {
            AllReads.Add(limit);
        }

        if (AllGate is not null)
        {
            await AllGate;
        }

        lock (_gate)
        {
            return Sales.Take(limit).ToList();
        }
    }

    public Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<DateOnly> days = Days.ToList();
            return Task.FromResult(days);
        }
    }

    public Task<long> CountAsync(int worldId, DateOnly day, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult((long)Sales.Count(s => s.WorldId == worldId && MannequinCql.DayOf(s.SaleTime) == day));
        }
    }

    public Task DeleteDayAsync(IReadOnlyCollection<int> worldIds, DateOnly day, CancellationToken ct = default)
    {
        lock (_gate)
        {
            Deletes.Add((worldIds, day));
            Sales.RemoveAll(s => worldIds.Contains(s.WorldId) && MannequinCql.DayOf(s.SaleTime) == day);
            Days.Remove(day);
        }
        return Task.CompletedTask;
    }
}
