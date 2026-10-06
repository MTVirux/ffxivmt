namespace Ffmt.Core.HealthChecks;

public sealed class WriteStallTracker
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _firstFailureSinceSuccess;
    private DateTimeOffset? _lastFailure;

    public void RecordSuccess(DateTimeOffset now)
    {
        lock (_lock)
        {
            _firstFailureSinceSuccess = null;
            _lastFailure = null;
        }
    }

    public void RecordFailure(DateTimeOffset now)
    {
        lock (_lock)
        {
            _firstFailureSinceSuccess ??= now;
            _lastFailure = now;
        }
    }

    public bool IsStalled(DateTimeOffset now, TimeSpan threshold)
    {
        lock (_lock)
        {
            return _firstFailureSinceSuccess is { } first
                && _lastFailure is { } last
                && now - first >= threshold
                && now - last < threshold;
        }
    }
}
