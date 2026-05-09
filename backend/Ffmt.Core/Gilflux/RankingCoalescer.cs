using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Ffmt.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed class RankingCoalescer : IHostedService
{
    private readonly IRankingRefresher _refresher;
    private readonly ILogger<RankingCoalescer> _logger;

    private readonly Channel<(int WorldId, int ItemId)> _channel;
    private readonly ConcurrentDictionary<(int, int), long> _lastFired = new();
    private readonly TimeSpan _coalesceWindow;
    private readonly int _workerCount;

    private long _droppedCoalesced;
    private long _droppedFull;

    private Timer? _logDropsTimer;
    private Task[] _workerTasks = [];
    private readonly CancellationTokenSource _stopCts = new();

    public RankingCoalescer(
        IRankingRefresher refresher,
        IOptions<GilfluxOptions> options,
        ILogger<RankingCoalescer> logger)
    {
        _refresher = refresher;
        _logger = logger;

        var opts = options.Value;
        _coalesceWindow = TimeSpan.FromSeconds(opts.CoalesceWindowSeconds);
        _workerCount = Math.Max(1, opts.CoalesceWorkers);

        _channel = Channel.CreateBounded<(int, int)>(new BoundedChannelOptions(Math.Max(1, opts.CoalesceQueueMax))
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = false
        });
    }

    /// <summary>Submit a (worldId, itemId) for refresh. Drops silently if recently fired or queue full.</summary>
    public void Submit(int worldId, int itemId)
    {
        var key = (worldId, itemId);

        if (_lastFired.TryGetValue(key, out var lastTick) &&
            Stopwatch.GetElapsedTime(lastTick) < _coalesceWindow)
        {
            Interlocked.Increment(ref _droppedCoalesced);
            return;
        }

        _lastFired[key] = Stopwatch.GetTimestamp();

        if (!_channel.Writer.TryWrite(key))
        {
            Interlocked.Increment(ref _droppedFull);
        }
    }

    public Task StartAsync(CancellationToken ct)
    {
        _workerTasks = Enumerable
            .Range(0, _workerCount)
            .Select(_ => Task.Run(() => WorkerLoop(_stopCts.Token), _stopCts.Token))
            .ToArray();

        _logDropsTimer = new Timer(LogDrops, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("RankingCoalescer started {Workers} worker(s)", _workerCount);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.Complete();
        _stopCts.Cancel();
        try { await Task.WhenAll(_workerTasks).WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _logDropsTimer?.Dispose();
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        await foreach (var (worldId, itemId) in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await _refresher.RefreshAsync(worldId, itemId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RankingCoalescer: refresh failed for world={WorldId} item={ItemId}", worldId, itemId);
            }
        }
    }

    private void LogDrops(object? state)
    {
        var coalesced = Interlocked.Exchange(ref _droppedCoalesced, 0);
        var full = Interlocked.Exchange(ref _droppedFull, 0);

        if (coalesced > 0 || full > 0)
        {
            _logger.LogInformation("RankingCoalescer drops in last 60s — coalesced: {Coalesced}, queue-full: {Full}", coalesced, full);
        }

        var cutoff = Stopwatch.GetTimestamp() - (long)(_coalesceWindow.TotalSeconds * 10 * Stopwatch.Frequency);
        foreach (var key in _lastFired.Keys)
        {
            if (_lastFired.TryGetValue(key, out var ts) && ts < cutoff)
            {
                _lastFired.TryRemove(key, out _);
            }
        }
    }
}
