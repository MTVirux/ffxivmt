using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Net.WebSockets;

namespace WsWorker.Workers;

public sealed class UniversalisWsConsumer : BackgroundService
{
    private readonly ISaleStore _saleStore;
    private readonly WorldStructureService _catalog;
    private readonly RankingCoalescer _coalescer;
    private readonly UniversalisOptions _options;
    private readonly ILogger<UniversalisWsConsumer> _logger;

    private int _inflightCount;

    private volatile bool _isConnected;
    public bool IsConnected => _isConnected;

    public UniversalisWsConsumer(
        ISaleStore saleStore,
        WorldStructureService catalog,
        RankingCoalescer coalescer,
        IOptions<UniversalisOptions> options,
        ILogger<UniversalisWsConsumer> logger)
    {
        _saleStore = saleStore;
        _catalog = catalog;
        _coalescer = coalescer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var worldsById = await _catalog.GetWorldsByIdAsync(ct);

        var worldIds = _options.RegionsToUse
            .SelectMany(region => worldsById.Values.Where(w => string.Equals(w.Region, region, StringComparison.OrdinalIgnoreCase)))
            .Select(w => w.Id)
            .Distinct()
            .ToList();

        _logger.LogInformation("UniversalisWsConsumer resolved {Count} worlds to subscribe to", worldIds.Count);

        var backoffSeconds = 1.0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConsumerLoop(worldIds, ct);
                backoffSeconds = 1.0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                var jitter = Random.Shared.NextDouble() * 2;
                var delay = backoffSeconds + jitter;
                _logger.LogWarning(ex, "WebSocket consumer loop failed — reconnecting in {Delay:F1}s", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                backoffSeconds = Math.Min(backoffSeconds * 2, 60.0);
            }
        }
    }

    private async Task ConsumerLoop(IReadOnlyList<int> worldIds, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        await ws.ConnectAsync(new Uri(_options.WsUrl), ct);
        _logger.LogInformation("Connected to Universalis WebSocket at {Url}", _options.WsUrl);

        foreach (var worldId in worldIds)
        {
            var subDoc = new BsonDocument
            {
                { "event", "subscribe" },
                { "channel", $"sales/add{{world={worldId}}}" }
            };
            var subBytes = subDoc.ToBson();
            await ws.SendAsync(
                new ArraySegment<byte>(subBytes),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                ct);
        }

        _logger.LogInformation("Subscribed to {Count} world channel(s)", worldIds.Count);
        _isConnected = true;

        var buffer = new byte[64 * 1024];
        using var messageStream = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            messageStream.SetLength(0);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _isConnected = false;
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    _logger.LogInformation("WebSocket closed by server");
                    return;
                }

                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            BsonDocument doc;
            try
            {
                messageStream.Position = 0;
                doc = BsonSerializer.Deserialize<BsonDocument>(messageStream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize BSON message ({ByteCount} bytes)", messageStream.Length);
                continue;
            }

            if (!doc.TryGetValue("event", out var eventVal) || !eventVal.IsString || eventVal.AsString != "sales/add")
                continue;

            if (!doc.TryGetValue("world", out var worldVal) || !doc.TryGetValue("item", out var itemVal))
                continue;

            int worldId, itemId;
            try
            {
                worldId = worldVal.ToInt32();
                itemId = itemVal.ToInt32();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse world/item from sales/add message");
                continue;
            }

            var world = await _catalog.GetWorldAsync(worldId, ct);
            if (world is null)
            {
                _logger.LogWarning("Received sales/add for unknown worldId {WorldId} — skipping", worldId);
                continue;
            }

            if (doc.TryGetValue("sales", out var salesVal) && salesVal.IsBsonArray)
            {
                var sales = new List<Sale>();
                foreach (BsonValue saleEntry in salesVal.AsBsonArray)
                {
                    if (saleEntry is not BsonDocument saleDoc)
                        continue;

                    var buyerName = saleDoc.TryGetValue("buyerName", out var bn) && bn.IsString
                        ? bn.AsString
                        : string.Empty;
                    if (string.IsNullOrEmpty(buyerName))
                        continue;

                    var hq = saleDoc.TryGetValue("hq", out var hqVal) && hqVal.IsBoolean && hqVal.AsBoolean;
                    var onMannequin = saleDoc.TryGetValue("onMannequin", out var omVal) && omVal.IsBoolean && omVal.AsBoolean;
                    var pricePerUnit = saleDoc.TryGetValue("pricePerUnit", out var ppuVal) ? ppuVal.ToInt32() : 0;
                    var quantity = saleDoc.TryGetValue("quantity", out var qVal) ? qVal.ToInt32() : 0;
                    var timestamp = saleDoc.TryGetValue("timestamp", out var tsVal) ? tsVal.ToInt64() : 0L;

                    sales.Add(new Sale(
                        ItemId:      itemId,
                        WorldId:     worldId,
                        BuyerName:   buyerName,
                        Hq:          hq,
                        OnMannequin: onMannequin,
                        Quantity:    quantity,
                        UnitPrice:   pricePerUnit,
                        SaleTime:    DateTimeOffset.FromUnixTimeSeconds(timestamp)));
                }

                if (sales.Count > 0)
                {
                    Interlocked.Increment(ref _inflightCount);
                    _ = _saleStore.AddBatchAsync(sales, ct).ContinueWith(t =>
                    {
                        Interlocked.Decrement(ref _inflightCount);
                        if (t.IsFaulted)
                            _logger.LogError(t.Exception, "Scylla fire-and-forget sale-batch insert failed");
                    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

                    if (_inflightCount > 500)
                        await Task.Yield();
                }
            }

            _coalescer.Submit(worldId, itemId);
        }

        _isConnected = false;
        _logger.LogInformation("WebSocket consumer loop exited (state={State})", ws.State);
    }
}
