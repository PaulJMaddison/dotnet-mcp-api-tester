namespace ApiTester.AI.Cost;

public static class AiCostCalculator
{
    public static ApiTester.AI.AiCostEstimate Estimate(string model, int inputTokens, int outputTokens)
    {
        if (string.Equals(model, "local-grounded", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiTester.AI.AiCostEstimate(
                InputCostUsd: 0m,
                OutputCostUsd: 0m,
                TotalCostUsd: 0m,
                IsKnown: true,
                Note: "Local fallback; no external model charge.");
        }

        // Azure/OpenAI pricing depends on the exact deployment, region, tier and
        // commercial terms. Do not turn token counts into invented billing data.
        return new ApiTester.AI.AiCostEstimate(
            InputCostUsd: 0m,
            OutputCostUsd: 0m,
            TotalCostUsd: 0m,
            IsKnown: false,
            Note: $"Pricing is not configured for model/deployment '{model}'.");
    }
}
