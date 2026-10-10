using System.Text.Json.Serialization;
using Ffmt.Core.External;

namespace Ffmt.Core.Status;

public sealed record StatusMetricsResponse(
    bool Available,
    string State,
    IReadOnlyList<string> Reasons,
    long GeneratedAt,
    IReadOnlyDictionary<string, MetricSnapshot> Metrics);

public sealed record MetricSnapshot(
    double? Value,
    IReadOnlyList<SeriesPoint> Series,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Total = null);
