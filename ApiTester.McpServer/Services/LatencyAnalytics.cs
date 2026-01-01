namespace ApiTester.McpServer.Services;

public sealed record LatencySnapshot(
    int SampleSize,
    double AverageMs,
    double? P50Ms,
    double? P95Ms,
    long MinMs,
    long MaxMs);

public sealed record RegressionAssessment(
    bool IsRegression,
    string Reason,
    double? BaselineP95Ms,
    double? CurrentP95Ms,
    double? DeltaMs,
    double? DeltaPercent);

public static class LatencyAnalytics
{
    public const double DefaultRegressionPercentThreshold = 0.20;
    public const double DefaultRegressionAbsoluteThresholdMs = 100;

    public static LatencySnapshot? BuildSnapshot(IEnumerable<long> samples)
    {
        if (samples is null)
            return null;

        var values = samples.Where(v => v >= 0).OrderBy(v => v).ToList();
        if (values.Count == 0)
            return null;

        var average = values.Average(v => (double)v);
        var p50 = ApproximatePercentile(values, 0.50);
        var p95 = ApproximatePercentile(values, 0.95);

        return new LatencySnapshot(
            values.Count,
            average,
            p50,
            p95,
            values[0],
            values[^1]);
    }

    public static double? ApproximatePercentile(IReadOnlyList<long> sortedValues, double percentile)
    {
        if (sortedValues is null || sortedValues.Count == 0)
            return null;

        if (percentile <= 0)
            return sortedValues[0];

        if (percentile >= 1)
            return sortedValues[^1];

        var position = percentile * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        var lowerValue = sortedValues[lowerIndex];
        var upperValue = sortedValues[upperIndex];
        var fraction = position - lowerIndex;

        return lowerValue + (upperValue - lowerValue) * fraction;
    }

    public static RegressionAssessment EvaluateRegression(
        LatencySnapshot? current,
        LatencySnapshot? baseline,
        double percentThreshold = DefaultRegressionPercentThreshold,
        double absoluteThresholdMs = DefaultRegressionAbsoluteThresholdMs)
    {
        if (current?.P95Ms is null || baseline?.P95Ms is null)
        {
            return new RegressionAssessment(
                false,
                "Insufficient percentile data for regression check.",
                baseline?.P95Ms,
                current?.P95Ms,
                null,
                null);
        }

        var delta = current.P95Ms.Value - baseline.P95Ms.Value;
        if (delta <= 0)
        {
            return new RegressionAssessment(
                false,
                "Latency improved or stayed flat versus baseline.",
                baseline.P95Ms,
                current.P95Ms,
                delta,
                baseline.P95Ms > 0 ? delta / baseline.P95Ms : null);
        }

        var percentDelta = baseline.P95Ms > 0
            ? delta / baseline.P95Ms.Value
            : (double?)null;

        var exceedsPercent = percentDelta.HasValue && percentDelta.Value >= percentThreshold;
        var exceedsAbsolute = delta >= absoluteThresholdMs;
        var isRegression = exceedsPercent || exceedsAbsolute;
        var reason = isRegression
            ? $"P95 increased by {delta:F1}ms ({(percentDelta.HasValue ? percentDelta.Value.ToString("P1") : "n/a")} vs baseline)."
            : "P95 increase within tolerance.";

        return new RegressionAssessment(
            isRegression,
            reason,
            baseline.P95Ms,
            current.P95Ms,
            delta,
            percentDelta);
    }
}
