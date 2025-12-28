using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence;

public sealed class ApiTesterDbContext : DbContext
{
    public ApiTesterDbContext(DbContextOptions<ApiTesterDbContext> options) : base(options) { }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<TestRunEntity> TestRuns => Set<TestRunEntity>();
    public DbSet<TestCaseResultEntity> TestCaseResults => Set<TestCaseResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(b =>
        {
            b.HasKey(x => x.ProjectId);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<TestRunEntity>(b =>
        {
            b.HasKey(x => x.RunId);
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.ProjectId, x.StartedUtc });

            b.HasOne(x => x.Project)
                .WithMany(p => p.Runs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestCaseResultEntity>(b =>
        {
            b.HasKey(x => x.TestCaseResultId);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Method).HasMaxLength(16).IsRequired();

            b.HasOne(x => x.Run)
                .WithMany(r => r.Results)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.RunId);
        });
    }
}
