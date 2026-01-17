using ApiTester.Site.Models;

namespace ApiTester.Site.Services;

public interface ILeadCaptureService
{
    Task<LeadCaptureResult> SubmitAsync(LeadCaptureRequest request, CancellationToken cancellationToken);
}
