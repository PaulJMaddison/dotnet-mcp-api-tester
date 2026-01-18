namespace ApiTester.McpServer.Models;

public sealed record ContractFailureReason(
    string Category,
    string Type,
    string Message,
    object? Details);
