namespace Ffmt.Core.External;

public interface IPrometheusClient
{
    Task<double?> QueryAsync(string promql, CancellationToken ct = default);

    Task<IReadOnlyList<SeriesPoint>> QueryRangeAsync(
        string promql, DateTimeOffset start, DateTimeOffset end, TimeSpan step, CancellationToken ct = default);
}

public sealed record SeriesPoint(long T, double? V);
