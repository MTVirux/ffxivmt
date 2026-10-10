namespace Ffmt.Core.Storage.Scylla;

/// <summary>Lazy rethrows a failed factory's exception forever. This one lets the next read run the
/// factory again, so a connect that timed out once doesn't take the API down until a restart.</summary>
internal sealed class RetryingLazy<T>
{
    private readonly Func<T> _factory;
    private Lazy<T> _lazy;

    public RetryingLazy(Func<T> factory)
    {
        _factory = factory;
        _lazy = NewLazy();
    }

    public bool IsValueCreated => _lazy.IsValueCreated;

    public T Value
    {
        get
        {
            var lazy = _lazy;
            try
            {
                return lazy.Value;
            }
            catch
            {
                Interlocked.CompareExchange(ref _lazy, NewLazy(), lazy);
                throw;
            }
        }
    }

    private Lazy<T> NewLazy() => new(_factory, LazyThreadSafetyMode.ExecutionAndPublication);
}
