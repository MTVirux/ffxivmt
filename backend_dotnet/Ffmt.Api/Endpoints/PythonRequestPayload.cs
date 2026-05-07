using System.Text.Json.Serialization;

namespace Ffmt.Api.Endpoints;

/// <summary>
/// Body shape posted by the Python sales importer to <c>/api/v1/updatedb/python_request</c>.
/// Mirrors the Universalis v2 history response: the <c>items</c> dictionary is keyed by item id
/// (as a string), each value carries an <c>entries</c> array of sale rows.
///
/// Property names are pinned with <see cref="JsonPropertyNameAttribute"/> because the upstream
/// uses camelCase (<c>worldID</c> at top level, <c>worldId</c> per entry), which the global
/// snake_case naming policy on the host would otherwise rewrite. Case-insensitive matching at
/// the host level covers minor capitalisation drift (<c>WorldID</c>, <c>worldid</c>, etc.).
/// </summary>
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
