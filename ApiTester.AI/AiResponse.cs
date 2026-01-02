namespace ApiTester.AI;

public sealed record AiResponse(
    string Content,
    AiUsage Usage,
    int ElapsedMs,
    string Model,
    AiCostEstimate Cost);
