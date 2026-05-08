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
        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaDb });

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

        var rows = new List<ItemUpsert>(50_000);
        string[]? line;
        while ((line = csv.ReadRow()) is not null)
        {
            var nameIdx = indexByColumn["Name"];
            if (nameIdx >= line.Length) continue;
            var name = Strip(line[nameIdx]);
            if (string.IsNullOrEmpty(name)) continue;

            rows.Add(new ItemUpsert(
                Id:                    GetInt(line, indexByColumn, "#"),
                Name:                  name,
                Description:           GetString(line, indexByColumn, "Description"),
                CanBeHq:               GetBool(line, indexByColumn, "CanBeHq"),
                AlwaysCollectible:     GetBool(line, indexByColumn, "AlwaysCollectable"),
                StackSize:             GetInt(line, indexByColumn, "StackSize"),
                ItemLevel:             GetInt(line, indexByColumn, "LevelItem"),
                IconImage:             GetInt(line, indexByColumn, "Icon"),
                Rarity:                GetInt(line, indexByColumn, "Rarity"),
                FilterGroup:           GetInt(line, indexByColumn, "FilterGroup"),
                ItemUiCategory:        GetInt(line, indexByColumn, "ItemUICategory"),
                ItemSearchCategory:    GetInt(line, indexByColumn, "ItemSearchCategory"),
                EquipSlotCategory:     GetInt(line, indexByColumn, "EquipSlotCategory"),
                Unique:                GetBool(line, indexByColumn, "IsUnique"),
                Untradable:            GetBool(line, indexByColumn, "IsUntradable"),
                Disposable:            GetBool(line, indexByColumn, "IsIndisposable"),
                Dyable:                GetInt(line, indexByColumn, "DyeCount") > 0,
                AetherialReductible:   GetInt(line, indexByColumn, "AetherialReduce") > 0,
                MateriaSlotCount:      GetInt(line, indexByColumn, "MateriaSlotCount"),
                AdvancedMelding:       GetBool(line, indexByColumn, "IsAdvancedMeldingPermitted")));
        }

        logger.LogInformation("Parsed {Count} items from CSV.", rows.Count);
        return rows;
    }

    private static string Strip(string raw) => raw.Trim().Trim('"');

    private static string GetString(string[] row, Dictionary<string, int> idx, string col)
        => idx.TryGetValue(col, out var i) && i < row.Length ? Strip(row[i]) : string.Empty;

    private static int GetInt(string[] row, Dictionary<string, int> idx, string col)
    {
        var s = GetString(row, idx, col);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static bool GetBool(string[] row, Dictionary<string, int> idx, string col)
        => string.Equals(GetString(row, idx, col), "True", StringComparison.OrdinalIgnoreCase);
}
