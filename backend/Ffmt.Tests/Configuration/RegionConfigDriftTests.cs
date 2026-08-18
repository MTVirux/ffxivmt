using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Ffmt.Tests.Configuration;

/// <summary>The region list lives in three deployment files and nothing reconciles them at runtime:
/// the compose env block is the only source Ffmt.Cli sees, and its indices override the API's
/// appsettings positionally - so a half-edit leaves each service ingesting a different set.</summary>
public sealed class RegionConfigDriftTests
{
    private static readonly string[] Ingested = ["Europe", "North-America", "Japan", "Oceania"];

    [Fact]
    public void Api_appsettings_lists_every_ingested_region()
    {
        JsonRegions("backend/Ffmt.Api/appsettings.json", "RegionsToUse").Should().Equal(Ingested);
    }

    [Fact]
    public void Compose_env_indices_match_the_api_region_list()
    {
        ComposeRegions().Should().Equal(Ingested);
    }

    [Fact]
    public void WsWorker_subscribes_to_and_backfills_the_same_regions()
    {
        const string path = "docker/ws_worker/WsWorker/appsettings.json";

        JsonRegions(path, "RegionsToUse").Should().Equal(Ingested);
        JsonRegions(path, "RegionsToImport").Should().Equal(Ingested);
    }

    private static string[] JsonRegions(string relativePath, string key) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), relativePath))
            .Build()
            .GetSection($"Universalis:{key}")
            .Get<string[]>() ?? [];

    private static IReadOnlyList<string> ComposeRegions()
    {
        const string prefix = "Universalis__RegionsToUse__";
        var byIndex = new SortedDictionary<int, string>();

        foreach (var raw in File.ReadAllLines(Path.Combine(RepoRoot(), "docker-compose.yml")))
        {
            var line = raw.Trim();
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var colon = line.IndexOf(':');
            var index = int.Parse(line[prefix.Length..colon], CultureInfo.InvariantCulture);
            byIndex[index] = line[(colon + 1)..].Trim();
        }

        return [.. byIndex.Values];
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException($"No docker-compose.yml found above {AppContext.BaseDirectory}.");
    }
}
