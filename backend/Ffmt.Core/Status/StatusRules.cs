using System.Globalization;

namespace Ffmt.Core.Status;

public sealed record StatusVerdict(string State, IReadOnlyList<string> Reasons);

public static class StatusRules
{
    public const double MaxErrorRate = 0.05;
    public const double MinWorldsConnectedRatio = 0.9;

    public static StatusVerdict Evaluate(double? storedBatchesLast10m, double? errorRate, double? worldsConnected, double? worldsTotal)
    {
        if (storedBatchesLast10m is null or <= 0)
        {
            return new StatusVerdict("down", ["No sales stored in the last 10 minutes"]);
        }

        var reasons = new List<string>();
        if (errorRate > MaxErrorRate)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"5xx error rate {errorRate.Value * 100:0.0}% (above 5%)"));
        }
        if (worldsConnected is { } connected && worldsTotal is > 0 and { } total && connected / total < MinWorldsConnectedRatio)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"{connected:0} of {total:0} worlds connected"));
        }

        return new StatusVerdict(reasons.Count == 0 ? "operational" : "degraded", reasons);
    }
}
