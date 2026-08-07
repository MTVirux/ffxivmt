using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Prometheus;
using System.Net.WebSockets;

namespace WsWorker.Workers;

public sealed class UniversalisWsConsumer : BackgroundService
{
    /// <summary>Prometheus takes a lock to resolve a label set, so the receive loop holds the
    /// children rather than resolving them per message.</summary>
    private sealed record WorldMetrics(Counter.Child Received, Counter.Child InsertOk, Counter.Child InsertError);

    /// <summary>The worlds this consumer subscribed to. A message for anything else is unexpected,
    /// so membership doubles as the validity check the loop used to ask the catalogue for.</summary>
    private sealed record Subscription(IReadOnlyList<int> WorldIds, IReadOnlyDictionary<int, WorldMetrics> Metrics);

    private readonly ISaleWriter _saleWriter;
    private readonly WorldStructureService _catalog;
    private readonly RankingCoalescer _coalescer;
    private readonly UniversalisOptions _options;
    private readonly ILogger<UniversalisWsConsumer> _logger;

    private Gauge.Child[] _connectedGauges = [];

    private volatile bool _isConnected;
    public bool IsConnected => _isConnected;

    public UniversalisWsConsumer(
        ISaleWriter saleWriter,
        WorldStructureService catalog,
        RankingCoalescer coalescer,
        IOptions<UniversalisOptions> options,
        ILogger<UniversalisWsConsumer> logger)
    {
        _saleWriter = saleWriter;
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

        var subscription = new Subscription(worldIds, worldIds.ToDictionary(id => id, id =>
        {
            var label = id.ToString();
            return new WorldMetrics(
                MetricsCatalog.WsSalesReceivedTotal.WithLabels(label),
                MetricsCatalog.WsInsertsTotal.WithLabels(label, "ok"),
                MetricsCatalog.WsInsertsTotal.WithLabels(label, "error"));
        }));

        _connectedGauges = worldIds.Select(id => MetricsCatalog.WsConnected.WithLabels(id.ToString())).ToArray();

        var backoffSeconds = 1.0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConsumerLoop(subscription, ct);
                backoffSeconds = 1.0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SetConnected(false);
                var jitter = Random.Shared.NextDouble() * 2;
                var delay = backoffSeconds + jitter;
                _logger.LogWarning(ex, "WebSocket consumer loop failed — reconnecting in {Delay:F1}s", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                backoffSeconds = Math.Min(backoffSeconds * 2, 60.0);
            }
        }
    }

    private void SetConnected(bool value)
    {
        _isConnected = value;
        var level = value ? 1 : 0;
        foreach (var gauge in _connectedGauges)
            gauge.Set(level);
    }

    private async Task ConsumerLoop(Subscription subscription, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        await ws.ConnectAsync(new Uri(_options.WsUrl), ct);
        _logger.LogInformation("Connected to Universalis WebSocket at {Url}", _options.WsUrl);

        foreach (var worldId in subscription.WorldIds)
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

        _logger.LogInformation("Subscribed to {Count} world channel(s)", subscription.WorldIds.Count);
        SetConnected(true);

        var buffer = new byte[64 * 1024];
        using var messageStream = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            messageStream.SetLength(0);

            ValueWebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer.AsMemory(), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    SetConnected(false);
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

            if (!subscription.Metrics.TryGetValue(worldId, out var worldMetrics))
            {
                _logger.LogWarning("Received sales/add for unknown worldId {WorldId} — skipping", worldId);
                continue;
            }

            if (doc.TryGetValue("sales", out var salesVal) && salesVal.IsBsonArray)
            {
                var salesArray = salesVal.AsBsonArray;
                var sales = new List<Sale>(salesArray.Count);
                foreach (BsonValue saleEntry in salesArray)
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
                    worldMetrics.Received.Inc(sales.Count);
                    _ = InsertAsync(sales, worldMetrics, ct);
                }
            }

            _coalescer.Submit(worldId, itemId);
        }

        SetConnected(false);
        _logger.LogInformation("WebSocket consumer loop exited (state={State})", ws.State);
    }

    /// <summary>Fire-and-forget by design: the receive loop must not block on Scylla acks during a
    /// sales burst. Failures surface through the metric and the worker's health check.</summary>
    private async Task InsertAsync(List<Sale> sales, WorldMetrics metrics, CancellationToken ct)
    {
        try
        {
            await _saleWriter.AddBatchAsync(sales, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            metrics.InsertError.Inc();
            _logger.LogError(ex, "Scylla fire-and-forget sale-batch insert failed");
            return;
        }

        metrics.InsertOk.Inc();
    }
}
