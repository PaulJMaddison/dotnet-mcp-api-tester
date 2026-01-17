namespace ApiTester.Site.Models;

public sealed class LeadCaptureSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Company { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; set; }
}
