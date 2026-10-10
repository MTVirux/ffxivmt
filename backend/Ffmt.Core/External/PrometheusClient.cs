using System.Globalization;
using System.Text.Json;

namespace Ffmt.Core.External;

public sealed class PrometheusClient(HttpClient http) : IPrometheusClient
{
    public const string HttpClientName = "prometheus";

    public async Task<double?> QueryAsync(string promql, CancellationToken ct = default)
    {
        using var doc = await GetAsync("api/v1/query?query=" + Uri.EscapeDataString(promql), ct).ConfigureAwait(false);
        var result = doc.RootElement.GetProperty("data").GetProperty("result");
        return result.GetArrayLength() == 0 ? null : ParseSample(result[0].GetProperty("value"));
    }

    public async Task<IReadOnlyList<SeriesPoint>> QueryRangeAsync(
        string promql, DateTimeOffset start, DateTimeOffset end, TimeSpan step, CancellationToken ct = default)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/v1/query_range?query={Uri.EscapeDataString(promql)}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={(long)step.TotalSeconds}");
        using var doc = await GetAsync(path, ct).ConfigureAwait(false);
        var result = doc.RootElement.GetProperty("data").GetProperty("result");
        if (result.GetArrayLength() == 0)
        {
            return [];
        }

        var values = result[0].GetProperty("values");
        var points = new List<SeriesPoint>(values.GetArrayLength());
        foreach (var sample in values.EnumerateArray())
        {
            points.Add(new SeriesPoint((long)sample[0].GetDouble(), ParseSample(sample)));
        }
        return points;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (doc.RootElement.GetProperty("status").GetString() != "success")
        {
            doc.Dispose();
            throw new InvalidOperationException($"Prometheus query failed: {path}");
        }
        return doc;
    }

    // Prometheus sends sample values as strings, including "NaN" and "+Inf".
    private static double? ParseSample(JsonElement sample) =>
        double.TryParse(sample[1].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)
            ? v
            : null;
}
