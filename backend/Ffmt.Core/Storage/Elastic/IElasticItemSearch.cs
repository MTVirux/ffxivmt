using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Elastic;

public interface IElasticItemSearch
{
    Task<IReadOnlyList<ElasticItemHit>> SearchByNameAsync(string query, int size, CancellationToken ct = default);

    Task UpsertAsync(int id, string name, CancellationToken ct = default);

    // One bulk round-trip per call; document ids match UpsertAsync.
    Task UpsertManyAsync(IEnumerable<(int Id, string Name)> items, CancellationToken ct = default);
}
