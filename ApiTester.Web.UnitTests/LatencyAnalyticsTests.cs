using ApiTester.McpServer.Services;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class LatencyAnalyticsTests
{
    [Fact]
    public void BuildSnapshot_EmptySamples_ReturnsNull()
    {
        var snapshot = LatencyAnalytics.BuildSnapshot(Array.Empty<long>());

        Assert.Null(snapshot);
    }

    [Fact]
    public void BuildSnapshot_ComputesPercentiles()
    {
        var snapshot = LatencyAnalytics.BuildSnapshot(new long[] { 100, 200, 300, 400, 500 });

        Assert.NotNull(snapshot);
        Assert.Equal(5, snapshot!.SampleSize);
        Assert.Equal(300, snapshot.P50Ms);
        Assert.Equal(480, snapshot.P95Ms);
    }

    [Fact]
    public void ApproximatePercentile_HandlesSingleValue()
    {
        var value = LatencyAnalytics.ApproximatePercentile(new List<long> { 42 }, 0.95);

        Assert.Equal(42, value);
    }

    [Fact]
    public void EvaluateRegression_FlagsPercentThreshold()
    {
        var baseline = LatencyAnalytics.BuildSnapshot(new long[] { 100, 110, 120, 130, 140 });
        var current = LatencyAnalytics.BuildSnapshot(new long[] { 140, 150, 160, 170, 180 });

        var assessment = LatencyAnalytics.EvaluateRegression(current, baseline, percentThreshold: 0.1, absoluteThresholdMs: 1000);

        Assert.True(assessment.IsRegression);
        Assert.Contains("P95 increased", assessment.Reason);
        Assert.True(assessment.DeltaPercent >= 0.1);
    }

    [Fact]
    public void EvaluateRegression_HandlesMissingPercentileData()
    {
        var assessment = LatencyAnalytics.EvaluateRegression(null, null);

        Assert.False(assessment.IsRegression);
        Assert.Contains("Insufficient", assessment.Reason);
    }
}
