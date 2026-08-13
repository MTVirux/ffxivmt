namespace Ffmt.Core.Configuration;

public sealed class UpdatedbOptions
{
    public const string SectionName = "Updatedb";

    // Candidates tried in order; the largest response wins.
    public string[] ItemCsvSources { get; init; } =
    [
        "https://raw.githubusercontent.com/MTVirux/ffxiv-datamining/master/csv/en/Item.csv",
        "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/Item.csv",
    ];

    public int GarlandBatchSize { get; init; } = 100;
}
