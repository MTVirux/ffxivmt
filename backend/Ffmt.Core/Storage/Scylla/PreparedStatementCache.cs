using System.Collections.Concurrent;
using Cassandra;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>The driver sends a PREPARE round trip on every Session.PrepareAsync call, so each CQL text
/// is prepared once here. A failed prepare is dropped so the next call retries it - a missing table
/// must start working once its CQL is applied.</summary>
internal sealed class PreparedStatementCache(Func<string, Task<PreparedStatement>> prepare)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PreparedStatement>>> _entries = new();

    public Task<PreparedStatement> GetAsync(string cql) =>
        _entries.GetOrAdd(cql, c => new Lazy<Task<PreparedStatement>>(() => PrepareOrForgetAsync(c))).Value;

    private async Task<PreparedStatement> PrepareOrForgetAsync(string cql)
    {
        try
        {
            return await prepare(cql).ConfigureAwait(false);
        }
        catch
        {
            _entries.TryRemove(cql, out _);
            throw;
        }
    }
}
