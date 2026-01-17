using ApiTester.Site.Models;

namespace ApiTester.Site.Services;

public interface ILeadCaptureStore
{
    Task AddAsync(LeadCaptureSubmission submission, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadCaptureSubmission>> GetAllAsync(CancellationToken cancellationToken);
}
