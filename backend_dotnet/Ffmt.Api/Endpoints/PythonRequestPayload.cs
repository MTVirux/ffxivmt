using System.Text.Json.Serialization;

namespace Ffmt.Api.Endpoints;

/// <summary>
/// Body shape posted by the Python sales importer to <c>/api/v1/updatedb/python_request</c>.
/// Mirrors the Universalis v2 history response: the <c>items</c> dictionary is keyed by item id
/// (as a string), each value carries an <c>entries</c> array of sale rows.
///
/// The hosting JSON options set <c>PropertyNameCaseInsensitive = true</c> so the upstream's mix
/// of <c>worldID</c> and <c>worldId</c> casings both bind to <see cref="WorldId"/>.
/// </summary>
public sealed class PythonRequestPayload
{
    public int? WorldId { get; set; }

    public int? ItemId { get; set; }

    public Dictionary<string, PythonRequestItem>? Items { get; set; }
}

public sealed class PythonRequestItem
{
    public List<PythonRequestEntry> Entries { get; set; } = [];
}

public sealed class PythonRequestEntry
{
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>Universalis sends <c>0</c> or <c>1</c>; converted to <see cref="bool"/> downstream.</summary>
    public int Hq { get; set; }

    public bool OnMannequin { get; set; }

    public int PricePerUnit { get; set; }

    public int Quantity { get; set; }

    /// <summary>Unix epoch seconds; converted to UTC <see cref="DateTimeOffset"/> downstream.</summary>
    public long Timestamp { get; set; }

    /// <summary>Per-entry world id (set on DC/region history responses); falls back to <see cref="PythonRequestPayload.WorldId"/>.</summary>
    public int? WorldId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}
