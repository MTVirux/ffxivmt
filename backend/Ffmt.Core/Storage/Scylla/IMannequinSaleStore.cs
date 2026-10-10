using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IMannequinSaleStore
{
    /// <summary>Ignores sales that are not on a mannequin.</summary>
    Task AddAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default);

    /// <summary>Every world's rows for one day older than <paramref name="before"/>, newest first.</summary>
    Task<IReadOnlyList<Sale>> GetByDayAsync(DateOnly day, DateTimeOffset before, CancellationToken ct = default);

    /// <summary>Up to <paramref name="limit"/> rows across every world and day, in no particular order.</summary>
    Task<IReadOnlyList<Sale>> GetAllAsync(int limit, CancellationToken ct = default);

    /// <summary>Days that hold rows, oldest first.</summary>
    Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken ct = default);

    Task<long> CountAsync(int worldId, DateOnly day, CancellationToken ct = default);

    /// <summary>Drops the day's partition for every given world, then its index row.</summary>
    Task DeleteDayAsync(IReadOnlyCollection<int> worldIds, DateOnly day, CancellationToken ct = default);
}
