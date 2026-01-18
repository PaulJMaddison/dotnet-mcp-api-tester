namespace ApiTester.Web.AI;

public sealed record AiRequest(
    string SystemPrompt,
    string UserPrompt,
    string? ModelId = null);
