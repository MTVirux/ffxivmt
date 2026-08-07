using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Elastic;

public interface IElasticItemSearch
{
    Task<IReadOnlyList<ElasticItemHit>> SearchByNameAsync(string query, int size, CancellationToken ct = default);

    Task UpsertAsync(int id, string name, CancellationToken ct = default);

    /// <summary>One bulk round-trip per call; document ids match <see cref="UpsertAsync"/>.</summary>
    Task UpsertManyAsync(IEnumerable<(int Id, string Name)> items, CancellationToken ct = default);
}
