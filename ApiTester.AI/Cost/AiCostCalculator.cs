namespace ApiTester.AI.Cost;

public static class AiCostCalculator
{
    // Simple illustrative rates per 1K tokens
    public static ApiTester.AI.AiCostEstimate Estimate(string model, int inputTokens, int outputTokens)
    {
        var (inRate, outRate) = model switch
        {
            "local-grounded-stub" => (0.0005m, 0.001m),
            _ => (0.002m, 0.002m)
        };

        var inCost = (inputTokens / 1000m) * inRate;
        var outCost = (outputTokens / 1000m) * outRate;

        var total = inCost + outCost;

        return new ApiTester.AI.AiCostEstimate(
            InputCostUsd: decimal.Round(inCost, 6),
            OutputCostUsd: decimal.Round(outCost, 6),
            TotalCostUsd: decimal.Round(total, 6));
    }
}
