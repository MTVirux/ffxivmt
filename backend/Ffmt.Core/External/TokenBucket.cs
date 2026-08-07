using System.Diagnostics;

namespace Ffmt.Core.External;

/// <summary>
/// Paces outbound Universalis calls: <c>capacity</c> requests may go out back to back, after which
/// the bucket only hands out tokens as fast as <c>refillRate</c> per second replaces them.
/// </summary>
public sealed class TokenBucket : IDisposable
{
    private readonly double _capacity;
    private readonly double _refillRate;
    private readonly Func<long> _timestamp;
    private double _tokens;
    private long _lastRefillTick;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TokenBucket(int capacity, int refillRate)
        : this(capacity, refillRate, Stopwatch.GetTimestamp)
    {
    }

    internal TokenBucket(int capacity, int refillRate, Func<long> timestamp)
    {
        _capacity = capacity;
        _refillRate = refillRate;
        _timestamp = timestamp;
        _tokens = capacity;
        _lastRefillTick = timestamp();
    }

    public async Task ConsumeAsync(CancellationToken ct)
    {
        while (true)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return;
                }
            }
            finally
            {
                _lock.Release();
            }

            var waitMs = (int)(1000.0 / _refillRate);
            await Task.Delay(waitMs, ct);
        }
    }

    private void Refill()
    {
        var now = _timestamp();
        var elapsed = (now - _lastRefillTick) / (double)Stopwatch.Frequency;
        _tokens = Math.Min(_capacity, _tokens + elapsed * _refillRate);
        _lastRefillTick = now;
    }

    public void Dispose() => _lock.Dispose();
}
