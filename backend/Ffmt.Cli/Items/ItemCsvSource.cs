using System.Globalization;
using System.Text;
using Ffmt.Core.Configuration;
using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Cli.Items;

public sealed class ItemCsvSource(
    IHttpClientFactory httpClientFactory,
    IOptions<UpdatedbOptions> options,
    ILogger<ItemCsvSource> logger)
{
    public const string HttpClientName = "item-csv";

    private static readonly string[] RequiredColumns =
    [
        "#", "Name", "Description", "CanBeHq", "AlwaysCollectable",
        "StackSize", "LevelItem", "Icon", "Rarity", "FilterGroup", "ItemUICategory",
        "ItemSearchCategory", "EquipSlotCategory", "IsUnique", "IsUntradable",
        "IsIndisposable", "DyeCount", "AetherialReduce", "MateriaSlotCount",
        "IsAdvancedMeldingPermitted",
    ];

    /// <summary>Downloads each configured CSV in parallel and parses the largest response.</summary>
    public async Task<IReadOnlyList<ItemUpsert>> LoadAsync(CancellationToken ct = default)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.ScyllaDb);

        var sources = options.Value.ItemCsvSources;
        if (sources.Length == 0)
        {
            throw new InvalidOperationException("No Updatedb:ItemCsvSources configured.");
        }

        var http = httpClientFactory.CreateClient(HttpClientName);
        var bodies = await Task.WhenAll(sources.Select(url => DownloadAsync(http, url, ct))).ConfigureAwait(false);

        var winnerIndex = 0;
        for (var i = 1; i < bodies.Length; i++)
        {
            if (bodies[i].Length > bodies[winnerIndex].Length)
            {
                winnerIndex = i;
            }
        }
        logger.LogInformation("CSV winner: {Url} ({Size} bytes); losers: {Losers}",
            sources[winnerIndex], bodies[winnerIndex].Length,
            string.Join(", ", sources.Where((_, i) => i != winnerIndex).Select((u, i) => $"{u}={bodies[i].Length}b")));

        return Parse(bodies[winnerIndex]);
    }

    private async Task<byte[]> DownloadAsync(HttpClient http, string url, CancellationToken ct)
    {
        logger.LogInformation("Downloading item CSV from {Url}.", url);
        var bytes = await http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        logger.LogInformation("Downloaded {Bytes} bytes from {Url}.", bytes.Length, url);
        return bytes;
    }

    private IReadOnlyList<ItemUpsert> Parse(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var reader = new StreamReader(ms, Encoding.UTF8);
        var csv = new CsvLineReader(reader);

        var header = csv.ReadRow() ?? throw new InvalidOperationException("Item CSV is empty.");
        var indexByColumn = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Length; i++)
        {
            indexByColumn[header[i].Trim('"')] = i;
        }

        foreach (var required in RequiredColumns)
        {
            if (!indexByColumn.ContainsKey(required))
            {
                throw new InvalidOperationException($"Item CSV is missing required column '{required}'. Headers: {string.Join(", ", header)}");
            }
        }

        // Every required column is present by now, so resolve each index once rather than
        // re-looking it up per field per row - ~900k dictionary probes across a full parse.
        var idCol = indexByColumn["#"];
        var nameCol = indexByColumn["Name"];
        var descriptionCol = indexByColumn["Description"];
        var canBeHqCol = indexByColumn["CanBeHq"];
        var alwaysCollectableCol = indexByColumn["AlwaysCollectable"];
        var stackSizeCol = indexByColumn["StackSize"];
        var levelItemCol = indexByColumn["LevelItem"];
        var iconCol = indexByColumn["Icon"];
        var rarityCol = indexByColumn["Rarity"];
        var filterGroupCol = indexByColumn["FilterGroup"];
        var itemUiCategoryCol = indexByColumn["ItemUICategory"];
        var itemSearchCategoryCol = indexByColumn["ItemSearchCategory"];
        var equipSlotCategoryCol = indexByColumn["EquipSlotCategory"];
        var isUniqueCol = indexByColumn["IsUnique"];
        var isUntradableCol = indexByColumn["IsUntradable"];
        var isIndisposableCol = indexByColumn["IsIndisposable"];
        var dyeCountCol = indexByColumn["DyeCount"];
        var aetherialReduceCol = indexByColumn["AetherialReduce"];
        var materiaSlotCountCol = indexByColumn["MateriaSlotCount"];
        var advancedMeldingCol = indexByColumn["IsAdvancedMeldingPermitted"];

        var rows = new List<ItemUpsert>(50_000);
        string[]? line;
        while ((line = csv.ReadRow()) is not null)
        {
            if (nameCol >= line.Length) continue;
            var name = Strip(line[nameCol]);
            if (string.IsNullOrEmpty(name)) continue;

            rows.Add(new ItemUpsert(
                Id:                    GetInt(line, idCol),
                Name:                  name,
                Description:           GetString(line, descriptionCol),
                CanBeHq:               GetBool(line, canBeHqCol),
                AlwaysCollectible:     GetBool(line, alwaysCollectableCol),
                StackSize:             GetInt(line, stackSizeCol),
                ItemLevel:             GetInt(line, levelItemCol),
                IconImage:             GetInt(line, iconCol),
                Rarity:                GetInt(line, rarityCol),
                FilterGroup:           GetInt(line, filterGroupCol),
                ItemUiCategory:        GetInt(line, itemUiCategoryCol),
                ItemSearchCategory:    GetInt(line, itemSearchCategoryCol),
                EquipSlotCategory:     GetInt(line, equipSlotCategoryCol),
                Unique:                GetBool(line, isUniqueCol),
                Untradable:            GetBool(line, isUntradableCol),
                Disposable:            GetBool(line, isIndisposableCol),
                Dyable:                GetInt(line, dyeCountCol) > 0,
                AetherialReductible:   GetInt(line, aetherialReduceCol) > 0,
                MateriaSlotCount:      GetInt(line, materiaSlotCountCol),
                AdvancedMelding:       GetBool(line, advancedMeldingCol)));
        }

        logger.LogInformation("Parsed {Count} items from CSV.", rows.Count);
        return rows;
    }

    private static string Strip(string raw) => raw.Trim().Trim('"');

    private static string GetString(string[] row, int col)
        => col < row.Length ? Strip(row[col]) : string.Empty;

    private static int GetInt(string[] row, int col)
        => int.TryParse(GetString(row, col), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static bool GetBool(string[] row, int col)
        => string.Equals(GetString(row, col), "True", StringComparison.OrdinalIgnoreCase);
}
