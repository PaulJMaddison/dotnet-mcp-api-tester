using System.ComponentModel.DataAnnotations;
using ApiTester.Site.Models;

namespace ApiTester.Site.Services;

public sealed class LeadCaptureService : ILeadCaptureService
{
    private readonly ILeadCaptureStore _store;

    public LeadCaptureService(ILeadCaptureStore store)
    {
        _store = store;
    }

    public async Task<LeadCaptureResult> SubmitAsync(LeadCaptureRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return LeadCaptureResult.HoneypotBlocked();
        }

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return LeadCaptureResult.Invalid(validationErrors);
        }

        var submission = new LeadCaptureSubmission
        {
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = request.Email!.Trim(),
            Company = request.Company?.Trim(),
            Message = request.Message!.Trim(),
            SubmittedAt = DateTimeOffset.UtcNow
        };

        await _store.AddAsync(submission, cancellationToken);

        return LeadCaptureResult.Accepted();
    }

    private static List<string> Validate(LeadCaptureRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(request);
        Validator.TryValidateObject(request, context, validationResults, true);

        return validationResults
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message!)
            .ToList();
    }
}
