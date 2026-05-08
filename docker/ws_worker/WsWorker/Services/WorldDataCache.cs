using WsWorker.Models;

namespace WsWorker.Services;

public sealed class WorldDataCache
{
    private sealed record CacheSnapshot(
        IReadOnlyDictionary<int, World> Worlds,
        IReadOnlyDictionary<int, string> ItemNames,
        IReadOnlyDictionary<int, string> MarketableItemNames,
        IReadOnlyList<int> MarketableItemIds,
        IReadOnlyList<string> Regions
    );

    private readonly ScyllaService _scylla;
    private readonly ILogger<WorldDataCache> _logger;

    private volatile CacheSnapshot _snapshot = null!;

    public IReadOnlyDictionary<int, World> Worlds => _snapshot.Worlds;
    public IReadOnlyDictionary<int, string> ItemNames => _snapshot.ItemNames;
    public IReadOnlyDictionary<int, string> MarketableItemNames => _snapshot.MarketableItemNames;
    public IReadOnlyList<int> MarketableItemIds => _snapshot.MarketableItemIds;
    public IReadOnlyList<string> Regions => _snapshot.Regions;

    public WorldDataCache(ScyllaService scylla, ILogger<WorldDataCache> logger)
    {
        _scylla = scylla;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var worldsStatement = await _scylla.PrepareAsync("SELECT id, name, datacenter, region FROM ffmt.worlds");
        var worldsResult = await _scylla.ExecuteAsync(worldsStatement.Bind());

        var worlds = new Dictionary<int, World>();
        foreach (var row in worldsResult)
        {
            var world = new World
            {
                Id = row.GetValue<int>("id"),
                Name = row.GetValue<string>("name"),
                Datacenter = row.GetValue<string>("datacenter"),
                Region = row.GetValue<string>("region"),
            };
            worlds[world.Id] = world;
        }

        var itemsStatement = await _scylla.PrepareAsync("SELECT id, name FROM ffmt.items");
        var itemsResult = await _scylla.ExecuteAsync(itemsStatement.Bind());

        var itemNames = new Dictionary<int, string>();
        foreach (var row in itemsResult)
        {
            var id = row.GetValue<int>("id");
            var name = row.GetValue<string>("name");
            itemNames[id] = name;
        }

        var marketableStatement = await _scylla.PrepareAsync("SELECT id, name FROM ffmt.items WHERE marketable = true ALLOW FILTERING");
        var marketableResult = await _scylla.ExecuteAsync(marketableStatement.Bind());

        var marketableItemNames = new Dictionary<int, string>();
        foreach (var row in marketableResult)
        {
            var id = row.GetValue<int>("id");
            var name = row.GetValue<string>("name");
            marketableItemNames[id] = name;
        }

        var marketableItemIds = marketableItemNames.Keys.OrderBy(id => id).ToList();
        var regions = worlds.Values.Select(w => w.Region).Distinct().ToList();

        _snapshot = new CacheSnapshot(worlds, itemNames, marketableItemNames, marketableItemIds, regions);

        _logger.LogInformation(
            "WorldDataCache loaded: {WorldCount} worlds, {ItemCount} items, {MarketableCount} marketable items, {RegionCount} regions",
            worlds.Count, itemNames.Count, marketableItemNames.Count, regions.Count);
    }

    public Task RefreshAsync() => InitializeAsync();

    public World? GetWorld(int worldId)
        => _snapshot.Worlds.TryGetValue(worldId, out var world) ? world : null;

    public string? GetItemName(int itemId)
        => _snapshot.ItemNames.TryGetValue(itemId, out var name) ? name : null;
}
