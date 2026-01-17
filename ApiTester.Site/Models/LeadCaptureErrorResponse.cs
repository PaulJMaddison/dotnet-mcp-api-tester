namespace ApiTester.Site.Models;

public sealed record LeadCaptureErrorResponse(IReadOnlyList<string> Errors);
