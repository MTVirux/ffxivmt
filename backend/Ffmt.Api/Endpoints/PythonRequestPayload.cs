using System.Text.Json.Serialization;

namespace Ffmt.Api.Endpoints;

// JsonPropertyName pins are required: upstream uses camelCase (worldID top-level, worldId
// per entry), which the global snake_case naming policy would otherwise rewrite.
public sealed class PythonRequestPayload
{
    [JsonPropertyName("worldID")]
    public int? WorldId { get; set; }

    [JsonPropertyName("itemID")]
    public int? ItemId { get; set; }

    [JsonPropertyName("items")]
    public Dictionary<string, PythonRequestItem>? Items { get; set; }
}

public sealed class PythonRequestItem
{
    [JsonPropertyName("entries")]
    public List<PythonRequestEntry> Entries { get; set; } = [];
}

public sealed class PythonRequestEntry
{
    [JsonPropertyName("buyerName")]
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>Universalis sends <c>0</c> or <c>1</c>; converted to <see cref="bool"/> downstream.</summary>
    [JsonPropertyName("hq")]
    public int Hq { get; set; }

    [JsonPropertyName("onMannequin")]
    public bool OnMannequin { get; set; }

    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    /// <summary>Unix epoch seconds; converted to UTC <see cref="DateTimeOffset"/> downstream.</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>Per-entry world id (set on DC/region history responses); falls back to <see cref="PythonRequestPayload.WorldId"/>.</summary>
    [JsonPropertyName("worldId")]
    public int? WorldId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}
