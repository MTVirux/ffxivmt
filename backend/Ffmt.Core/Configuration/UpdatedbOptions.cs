namespace Ffmt.Core.Configuration;

/// <summary>
/// Tunables for the <c>updatedb</c> CLI: where to fetch the FFXIV item CSV from, and how
/// large each Garland batch is.
/// </summary>
public sealed class UpdatedbOptions
{
    public const string SectionName = "Updatedb";

    /// <summary>
    /// Candidate URLs for the FFXIV datamining item CSV. The CLI downloads each in parallel
    /// and uses the larger one (matches the legacy PHP "pick the bigger file" heuristic).
    /// </summary>
    public string[] ItemCsvSources { get; init; } =
    [
        "https://raw.githubusercontent.com/MTVirux/ffxiv-datamining/master/csv/en/Item.csv",
        "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/Item.csv",
    ];

    /// <summary>Number of item ids per Garland batch request (PHP used 100).</summary>
    public int GarlandBatchSize { get; init; } = 100;
}
