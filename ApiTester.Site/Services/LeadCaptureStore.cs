using ApiTester.Site.Data;
using ApiTester.Site.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Site.Services;

public sealed class LeadCaptureStore : ILeadCaptureStore
{
    private readonly LeadCaptureDbContext _dbContext;

    public LeadCaptureStore(LeadCaptureDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LeadCaptureSubmission submission, CancellationToken cancellationToken)
    {
        _dbContext.LeadSubmissions.Add(submission);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeadCaptureSubmission>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.LeadSubmissions
            .AsNoTracking()
            .OrderByDescending(submission => submission.SubmittedAt)
            .ToListAsync(cancellationToken);
    }
}
