using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence;

public sealed class ApiTesterDbContext : DbContext
{
    public ApiTesterDbContext(DbContextOptions<ApiTesterDbContext> options) : base(options) { }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<TestRunEntity> TestRuns => Set<TestRunEntity>();
    public DbSet<TestCaseResultEntity> TestCaseResults => Set<TestCaseResultEntity>();
    public DbSet<OpenApiSpecEntity> OpenApiSpecs => Set<OpenApiSpecEntity>();
    public DbSet<TestPlanEntity> TestPlans => Set<TestPlanEntity>();
    public DbSet<EnvironmentEntity> Environments => Set<EnvironmentEntity>();

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
            b.HasIndex(x => x.BaselineRunId);
            b.HasIndex(x => x.SpecId);

            b.HasOne(x => x.Project)
                .WithMany(p => p.Runs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Spec)
                .WithMany()
                .HasForeignKey(x => x.SpecId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.BaselineRun)
                .WithMany()
                .HasForeignKey(x => x.BaselineRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectEntity>(b =>
        {
            b.HasKey(x => x.ProjectId);

            b.Property(x => x.OwnerKey)
                .HasMaxLength(100)
                .IsRequired();

            b.Property(x => x.ProjectKey)
                .HasMaxLength(100)
                .IsRequired();

            b.HasIndex(x => new { x.OwnerKey, x.ProjectKey }).IsUnique();
            b.HasIndex(x => x.OwnerKey);
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

        modelBuilder.Entity<OpenApiSpecEntity>(b =>
        {
            b.HasKey(x => x.SpecId);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasMaxLength(50).IsRequired();
            b.Property(x => x.SpecJson).IsRequired();
            b.Property(x => x.SpecHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => new { x.ProjectId, x.SpecHash }).IsUnique();

            b.HasOne(x => x.Project)
                .WithMany(p => p.OpenApiSpecs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestPlanEntity>(b =>
        {
            b.HasKey(x => new { x.ProjectId, x.OperationId });
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.Property(x => x.PlanJson).IsRequired();
            b.HasIndex(x => x.ProjectId);

            b.HasOne(x => x.Project)
                .WithMany(p => p.TestPlans)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EnvironmentEntity>(b =>
        {
            b.HasKey(x => x.EnvironmentId);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.BaseUrl).HasMaxLength(2048).IsRequired();
            b.Property(x => x.OwnerKey).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OwnerKey, x.ProjectId, x.Name }).IsUnique();
            b.HasIndex(x => x.ProjectId);

            b.HasOne(x => x.Project)
                .WithMany(p => p.Environments)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
