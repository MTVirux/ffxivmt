using WsWorker.Models;

namespace WsWorker.Services;

public sealed class WorldDataCache
{
    private readonly ScyllaService _scylla;
    private readonly ILogger<WorldDataCache> _logger;

    private IReadOnlyDictionary<int, World> _worlds = new Dictionary<int, World>();
    private IReadOnlyDictionary<int, string> _itemNames = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, string> _marketableItemNames = new Dictionary<int, string>();
    private IReadOnlyList<int> _marketableItemIds = Array.Empty<int>();
    private IReadOnlyList<string> _regions = Array.Empty<string>();

    public IReadOnlyDictionary<int, World> Worlds => _worlds;
    public IReadOnlyDictionary<int, string> ItemNames => _itemNames;
    public IReadOnlyDictionary<int, string> MarketableItemNames => _marketableItemNames;
    public IReadOnlyList<int> MarketableItemIds => _marketableItemIds;
    public IReadOnlyList<string> Regions => _regions;

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

        _worlds = worlds;
        _itemNames = itemNames;
        _marketableItemNames = marketableItemNames;
        _marketableItemIds = marketableItemIds;
        _regions = regions;

        _logger.LogInformation(
            "WorldDataCache loaded: {WorldCount} worlds, {ItemCount} items, {MarketableCount} marketable items, {RegionCount} regions",
            worlds.Count, itemNames.Count, marketableItemNames.Count, regions.Count);
    }

    public Task RefreshAsync() => InitializeAsync();

    public World? GetWorld(int worldId)
        => _worlds.TryGetValue(worldId, out var world) ? world : null;

    public string? GetItemName(int itemId)
        => _itemNames.TryGetValue(itemId, out var name) ? name : null;
}
