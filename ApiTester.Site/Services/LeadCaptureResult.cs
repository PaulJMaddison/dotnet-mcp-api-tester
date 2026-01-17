namespace ApiTester.Site.Services;

public sealed record LeadCaptureResult(bool IsAccepted, bool IsHoneypot, IReadOnlyList<string> Errors)
{
    public static LeadCaptureResult Accepted()
        => new(true, false, Array.Empty<string>());

    public static LeadCaptureResult HoneypotBlocked()
        => new(false, true, Array.Empty<string>());

    public static LeadCaptureResult Invalid(IReadOnlyList<string> errors)
        => new(false, false, errors);
}
