using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using WsWorker.Options;

namespace WsWorker.Services;

public sealed class GilfluxCoalescer : IHostedService
{
    private readonly GilfluxOptions _options;
    private readonly string _backendHost;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GilfluxCoalescer> _logger;

    private readonly Channel<(int WorldId, int ItemId)> _channel;
    private readonly ConcurrentDictionary<(int, int), long> _lastFired = new();
    private readonly TimeSpan _coalesceWindow;

    private long _droppedCoalesced;
    private long _droppedFull;

    private Timer? _logDropsTimer;
    private Task[] _workerTasks = [];

    public GilfluxCoalescer(
        IOptions<GilfluxOptions> options,
        IOptions<BackendOptions> backendOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<GilfluxCoalescer> logger)
    {
        _options = options.Value;
        _backendHost = backendOptions.Value.Host;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _coalesceWindow = TimeSpan.FromSeconds(_options.CoalesceWindowSeconds);

        _channel = Channel.CreateBounded<(int, int)>(new BoundedChannelOptions(_options.QueueMax)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = false
        });
    }

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
            Interlocked.Increment(ref _droppedFull);
    }

    public Task StartAsync(CancellationToken ct)
    {
        _workerTasks = Enumerable
            .Range(0, _options.Workers)
            .Select(_ => Task.Run(() => WorkerLoop(ct), ct))
            .ToArray();

        _logDropsTimer = new Timer(LogDrops, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("GilfluxCoalescer started {Workers} worker(s)", _options.Workers);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.Complete();
        try { await Task.WhenAll(_workerTasks).WaitAsync(ct); }
        catch (OperationCanceledException) { }
        _logDropsTimer?.Dispose();
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("gilflux");

        await foreach (var (worldId, itemId) in _channel.Reader.ReadAllAsync(ct))
        {
            var url = $"http://{_backendHost}/api/v1/updatedb/gilflux_ranking_update/{worldId}/{itemId}";
            try
            {
                using var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Gilflux ranking update returned {StatusCode} for {Url}", (int)response.StatusCode, url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Gilflux HTTP request failed for {Url}: {Message}", url, ex.Message);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Gilflux HTTP timeout for {Url}: {Message}", url, ex.Message);
            }
        }
    }

    private void LogDrops(object? state)
    {
        var coalesced = Interlocked.Exchange(ref _droppedCoalesced, 0);
        var full = Interlocked.Exchange(ref _droppedFull, 0);

        if (coalesced > 0 || full > 0)
            _logger.LogInformation("GilfluxCoalescer drops in last 60s — coalesced: {Coalesced}, queue-full: {Full}", coalesced, full);

        var cutoff = Stopwatch.GetTimestamp() - (long)(_coalesceWindow.TotalSeconds * 10 * Stopwatch.Frequency);
        foreach (var key in _lastFired.Keys)
        {
            if (_lastFired.TryGetValue(key, out var ts) && ts < cutoff)
                _lastFired.TryRemove(key, out _);
        }
    }
}
