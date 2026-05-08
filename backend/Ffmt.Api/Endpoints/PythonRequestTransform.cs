using Ffmt.Core.Models;

namespace Ffmt.Api.Endpoints;

public static class PythonRequestTransform
{
    public sealed record Result(IReadOnlyList<Sale> Sales, IReadOnlySet<(int WorldId, int ItemId)> RankingPairs);

    public static Result Build(
        PythonRequestPayload payload,
        IReadOnlyDictionary<int, World> worldsById,
        IReadOnlyDictionary<int, string> itemNamesById)
    {
        if (payload.Items is null || payload.Items.Count == 0)
        {
            return new Result(Array.Empty<Sale>(), new HashSet<(int, int)>());
        }

        var sales = new List<Sale>();
        var pairs = new HashSet<(int WorldId, int ItemId)>();

        foreach (var (itemKey, group) in payload.Items)
        {
            if (!int.TryParse(itemKey, out var itemId))
            {
                continue;
            }

            foreach (var entry in group.Entries)
            {
                var worldId = entry.WorldId ?? payload.WorldId;
                if (worldId is null)
                {
                    continue;
                }

                if (!worldsById.TryGetValue(worldId.Value, out var world))
                {
                    continue;
                }

                var itemName = itemNamesById.TryGetValue(itemId, out var n) ? n : string.Empty;

                sales.Add(new Sale(
                    ItemId: itemId,
                    WorldId: worldId.Value,
                    ItemName: itemName,
                    WorldName: world.Name,
                    Datacenter: world.Datacenter,
                    Region: world.Region,
                    BuyerName: entry.BuyerName,
                    Hq: entry.Hq == 1,
                    OnMannequin: entry.OnMannequin,
                    Quantity: entry.Quantity,
                    UnitPrice: entry.PricePerUnit,
                    Total: entry.Quantity * entry.PricePerUnit,
                    SaleTime: DateTimeOffset.FromUnixTimeSeconds(entry.Timestamp)));

                pairs.Add((worldId.Value, itemId));
            }
        }

        return new Result(sales, pairs);
    }
}
