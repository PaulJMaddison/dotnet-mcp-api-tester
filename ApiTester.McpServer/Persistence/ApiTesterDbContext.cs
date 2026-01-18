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
    public DbSet<TestPlanDraftEntity> TestPlanDrafts => Set<TestPlanDraftEntity>();
    public DbSet<EnvironmentEntity> Environments => Set<EnvironmentEntity>();
    public DbSet<RunAnnotationEntity> RunAnnotations => Set<RunAnnotationEntity>();
    public DbSet<OrganisationEntity> Organisations => Set<OrganisationEntity>();
    public DbSet<AiInsightEntity> AiInsights => Set<AiInsightEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<MembershipEntity> Memberships => Set<MembershipEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<BaselineRunEntity> BaselineRuns => Set<BaselineRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(b =>
        {
            b.HasKey(x => x.ProjectId);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.Name);
            b.HasIndex(x => x.OrganisationId);

            b.HasOne(x => x.Organisation)
                .WithMany(o => o.Projects)
                .HasForeignKey(x => x.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestRunEntity>(b =>
        {
            b.HasKey(x => x.RunId);
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.Property(x => x.Actor).HasMaxLength(100);
            b.Property(x => x.EnvironmentName).HasMaxLength(100);
            b.Property(x => x.EnvironmentBaseUrl).HasMaxLength(2048);
            b.HasIndex(x => new { x.ProjectId, x.StartedUtc });
            b.HasIndex(x => x.BaselineRunId);
            b.HasIndex(x => x.SpecId);
            b.HasIndex(x => x.OrganisationId);

            b.HasOne(x => x.Project)
                .WithMany(p => p.Runs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Organisation)
                .WithMany(o => o.Runs)
                .HasForeignKey(x => x.OrganisationId)
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

        modelBuilder.Entity<BaselineRunEntity>(b =>
        {
            b.HasKey(x => new { x.ProjectId, x.OperationId });
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.RunId);

            b.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Run)
                .WithMany()
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
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

            b.HasIndex(x => new { x.OrganisationId, x.ProjectKey }).IsUnique();
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

        modelBuilder.Entity<AuditEventEntity>(b =>
        {
            b.HasKey(x => x.AuditEventId);
            b.Property(x => x.Action).HasMaxLength(120).IsRequired();
            b.Property(x => x.TargetType).HasMaxLength(100).IsRequired();
            b.Property(x => x.TargetId).HasMaxLength(200).IsRequired();
            b.Property(x => x.MetadataJson);
            b.HasIndex(x => new { x.OrganisationId, x.CreatedUtc });
            b.HasIndex(x => new { x.OrganisationId, x.Action, x.CreatedUtc });

            b.HasOne(x => x.Organisation)
                .WithMany(o => o.AuditEvents)
                .HasForeignKey(x => x.OrganisationId)
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

        modelBuilder.Entity<TestPlanDraftEntity>(b =>
        {
            b.HasKey(x => x.DraftId);
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.Property(x => x.PlanJson).IsRequired();
            b.HasIndex(x => x.ProjectId);

            b.HasOne(x => x.Project)
                .WithMany(p => p.TestPlanDrafts)
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

        modelBuilder.Entity<RunAnnotationEntity>(b =>
        {
            b.HasKey(x => x.AnnotationId);
            b.Property(x => x.OwnerKey).HasMaxLength(100).IsRequired();
            b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
            b.Property(x => x.JiraLink).HasMaxLength(2048);
            b.HasIndex(x => x.RunId);
            b.HasIndex(x => new { x.OwnerKey, x.RunId });

            b.HasOne(x => x.Run)
                .WithMany(r => r.Annotations)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganisationEntity>(b =>
        {
            b.HasKey(x => x.OrganisationId);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            b.Property(x => x.RedactionRulesJson);
            b.Property(x => x.OrgSettingsJson);
            b.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.HasKey(x => x.UserId);
            b.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Email).HasMaxLength(320);
            b.HasIndex(x => x.ExternalId).IsUnique();
        });

        modelBuilder.Entity<MembershipEntity>(b =>
        {
            b.HasKey(x => new { x.OrganisationId, x.UserId });
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40).IsRequired();

            b.HasOne(x => x.Organisation)
                .WithMany(o => o.Memberships)
                .HasForeignKey(x => x.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiInsightEntity>(b =>
        {
            b.HasKey(x => x.InsightId);
            b.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasMaxLength(120).IsRequired();
            b.Property(x => x.ModelId).HasMaxLength(120).IsRequired();
            b.Property(x => x.JsonPayload).IsRequired();
            b.HasIndex(x => new { x.OrganisationId, x.ProjectId, x.RunId });
            b.HasIndex(x => new { x.OrganisationId, x.OperationId });
        });

        modelBuilder.Entity<ApiKeyEntity>(b =>
        {
            b.HasKey(x => x.KeyId);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Scopes).HasMaxLength(400).IsRequired();
            b.Property(x => x.Hash).HasMaxLength(128).IsRequired();
            b.Property(x => x.Prefix).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.Prefix).IsUnique();
            b.HasIndex(x => x.OrganisationId);
            b.HasIndex(x => x.UserId);

            b.HasOne(x => x.Organisation)
                .WithMany()
                .HasForeignKey(x => x.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
