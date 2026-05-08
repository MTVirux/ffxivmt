using System.Collections.Concurrent;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>
/// Coalesces identical in-flight Scylla reads so concurrent callers asking for the same
/// key share a single Task rather than each issuing their own query.
/// </summary>
internal sealed class RequestCoalescer<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Task<TValue>> _inflight = new();

    public Task<TValue> CoalesceAsync(TKey key, Func<Task<TValue>> factory)
    {
        if (_inflight.TryGetValue(key, out var existing) && !existing.IsCompleted)
            return existing;

        var task = factory();
        _inflight[key] = task;
        _ = task.ContinueWith(_ => _inflight.TryRemove(key, out _!), TaskScheduler.Default);
        return task;
    }
}
