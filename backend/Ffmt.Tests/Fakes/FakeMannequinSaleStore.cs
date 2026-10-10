using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Fakes;

// Partitions like the Scylla table so reader and cap tests exercise real day boundaries.
internal sealed class FakeMannequinSaleStore : IMannequinSaleStore
{
    private readonly object _gate = new();

    public List<Sale> Sales { get; } = [];
    public List<(int WorldId, DateOnly Day)> Reads { get; } = [];
    public List<(IReadOnlyCollection<int> WorldIds, DateOnly Day)> Deletes { get; } = [];

    public Task AddAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        lock (_gate)
        {
            Sales.AddRange(sales.Where(s => s.OnMannequin));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Sale>> GetByWorldAndDayAsync(
        int worldId, DateOnly day, DateTimeOffset before, CancellationToken ct = default)
    {
        lock (_gate)
        {
            Reads.Add((worldId, day));
            IReadOnlyList<Sale> rows = Sales
                .Where(s => s.WorldId == worldId && MannequinCql.DayOf(s.SaleTime) == day && s.SaleTime < before)
                .OrderByDescending(s => s.SaleTime)
                .ToList();
            return Task.FromResult(rows);
        }
    }

    public Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<DateOnly> days = Sales.Select(s => MannequinCql.DayOf(s.SaleTime)).Distinct().Order().ToList();
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
        }
        return Task.CompletedTask;
    }
}
