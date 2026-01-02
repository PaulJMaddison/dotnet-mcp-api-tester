namespace ApiTester.AI;

public sealed record AiCostEstimate(
    decimal InputCostUsd,
    decimal OutputCostUsd,
    decimal TotalCostUsd);
