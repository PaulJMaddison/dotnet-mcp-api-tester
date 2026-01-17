using ApiTester.Site.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Site.Data;

public sealed class LeadCaptureDbContext : DbContext
{
    public LeadCaptureDbContext(DbContextOptions<LeadCaptureDbContext> options)
        : base(options)
    {
    }

    public DbSet<LeadCaptureSubmission> LeadSubmissions => Set<LeadCaptureSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadCaptureSubmission>(entity =>
        {
            entity.HasKey(submission => submission.Id);
            entity.Property(submission => submission.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(submission => submission.LastName).HasMaxLength(80).IsRequired();
            entity.Property(submission => submission.Email).HasMaxLength(200).IsRequired();
            entity.Property(submission => submission.Company).HasMaxLength(120);
            entity.Property(submission => submission.Message).HasMaxLength(500).IsRequired();
            entity.Property(submission => submission.SubmittedAt).IsRequired();
        });
    }
}
