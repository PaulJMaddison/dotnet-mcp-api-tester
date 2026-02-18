/*
    ApiTester SQL Server schema bootstrap (idempotent)

    Creates the full ApiTester relational schema from scratch:
    - tables, primary keys, foreign keys, defaults, and indexes
    - explicit delete behaviors aligned to EF Core model

    How to run:
      - SSMS: open this file and execute.
      - sqlcmd: sqlcmd -S <server> -d <database> -i db/ApiTester.sqlserver.schema.sql

    Assumptions:
      - By default this script targets the current database context.
      - Optional database creation block is included below (commented out).

    Delete behavior choice:
      - Cascades are used where configured in EF (Project -> Runs -> Results, etc.) to keep dependent data consistent.
      - SET NULL is used for optional TestRuns.SpecId and TestRuns.BaselineRunId relationships.
*/

/* Optional database creation
IF DB_ID(N'ApiTester') IS NULL
BEGIN
    CREATE DATABASE [ApiTester];
END;
GO
USE [ApiTester];
GO
*/

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'dbo')
BEGIN
    EXEC('CREATE SCHEMA [dbo]');
END;
GO

/* Core identity/tenant tables */
IF OBJECT_ID(N'[dbo].[Organisations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Organisations]
    (
        [OrganisationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Organisations_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [RetentionDays] int NULL,
        [RedactionRulesJson] nvarchar(max) NULL,
        [OrgSettingsJson] nvarchar(max) NULL,
        CONSTRAINT [PK_Organisations] PRIMARY KEY ([OrganisationId])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [UserId] uniqueidentifier NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Users_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Projects]
    (
        [ProjectId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [OwnerKey] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Projects_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [ProjectKey] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([ProjectId]),
        CONSTRAINT [FK_Projects_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE
    );
END;
GO

/* API specs and runs */
IF OBJECT_ID(N'[dbo].[OpenApiSpecs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OpenApiSpecs]
    (
        [SpecId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Version] nvarchar(50) NOT NULL,
        [SpecJson] nvarchar(max) NOT NULL,
        [SpecHash] nvarchar(64) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_OpenApiSpecs_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_OpenApiSpecs] PRIMARY KEY ([SpecId]),
        CONSTRAINT [FK_OpenApiSpecs_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[TestRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TestRuns]
    (
        [RunId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SpecId] uniqueidentifier NULL,
        [BaselineRunId] uniqueidentifier NULL,
        [Actor] nvarchar(100) NULL,
        [EnvironmentName] nvarchar(100) NULL,
        [EnvironmentBaseUrl] nvarchar(2048) NULL,
        [PolicySnapshotJson] nvarchar(max) NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [StartedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NOT NULL,
        [TotalCases] int NOT NULL CONSTRAINT [DF_TestRuns_TotalCases] DEFAULT (0),
        [Passed] int NOT NULL CONSTRAINT [DF_TestRuns_Passed] DEFAULT (0),
        [Failed] int NOT NULL CONSTRAINT [DF_TestRuns_Failed] DEFAULT (0),
        [Blocked] int NOT NULL CONSTRAINT [DF_TestRuns_Blocked] DEFAULT (0),
        [TotalDurationMs] bigint NOT NULL CONSTRAINT [DF_TestRuns_TotalDurationMs] DEFAULT (0),
        CONSTRAINT [PK_TestRuns] PRIMARY KEY ([RunId]),
        CONSTRAINT [FK_TestRuns_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_TestRuns_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE,
        CONSTRAINT [FK_TestRuns_OpenApiSpecs_SpecId]
            FOREIGN KEY ([SpecId]) REFERENCES [dbo].[OpenApiSpecs]([SpecId]) ON DELETE SET NULL,
        CONSTRAINT [FK_TestRuns_TestRuns_BaselineRunId]
            FOREIGN KEY ([BaselineRunId]) REFERENCES [dbo].[TestRuns]([RunId]) ON DELETE SET NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[TestCaseResults]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TestCaseResults]
    (
        [TestCaseResultId] bigint NOT NULL IDENTITY(1,1),
        [RunId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Blocked] bit NOT NULL CONSTRAINT [DF_TestCaseResults_Blocked] DEFAULT (0),
        [BlockReason] nvarchar(max) NULL,
        [Method] nvarchar(16) NOT NULL,
        [Url] nvarchar(max) NULL,
        [StatusCode] int NULL,
        [DurationMs] bigint NOT NULL CONSTRAINT [DF_TestCaseResults_DurationMs] DEFAULT (0),
        [Pass] bit NOT NULL CONSTRAINT [DF_TestCaseResults_Pass] DEFAULT (0),
        [FailureReason] nvarchar(max) NULL,
        [ResponseSnippet] nvarchar(max) NULL,
        [IsFlaky] bit NOT NULL CONSTRAINT [DF_TestCaseResults_IsFlaky] DEFAULT (0),
        [FlakeReasonCategory] nvarchar(max) NULL,
        [Classification] int NULL,
        CONSTRAINT [PK_TestCaseResults] PRIMARY KEY ([TestCaseResultId]),
        CONSTRAINT [FK_TestCaseResults_TestRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[TestRuns]([RunId]) ON DELETE CASCADE
    );
END;
GO

/* Planning and annotations */
IF OBJECT_ID(N'[dbo].[TestPlans]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TestPlans]
    (
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [PlanJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_TestPlans_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TestPlans] PRIMARY KEY ([ProjectId], [OperationId]),
        CONSTRAINT [FK_TestPlans_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[TestPlanDrafts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TestPlanDrafts]
    (
        [DraftId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [PlanJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_TestPlanDrafts_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TestPlanDrafts] PRIMARY KEY ([DraftId]),
        CONSTRAINT [FK_TestPlanDrafts_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[RunAnnotations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RunAnnotations]
    (
        [AnnotationId] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [OwnerKey] nvarchar(100) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [JiraLink] nvarchar(2048) NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_RunAnnotations_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_RunAnnotations_UpdatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_RunAnnotations] PRIMARY KEY ([AnnotationId]),
        CONSTRAINT [FK_RunAnnotations_TestRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[TestRuns]([RunId]) ON DELETE CASCADE
    );
END;
GO

/* Membership, auth, and audit */
IF OBJECT_ID(N'[dbo].[Memberships]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Memberships]
    (
        [OrganisationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Memberships_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Memberships] PRIMARY KEY ([OrganisationId], [UserId]),
        CONSTRAINT [FK_Memberships_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Memberships_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[AuditEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditEvents]
    (
        [AuditEventId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [TargetType] nvarchar(100) NOT NULL,
        [TargetId] nvarchar(200) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_AuditEvents_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [MetadataJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([AuditEventId]),
        CONSTRAINT [FK_AuditEvents_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[ApiKeys]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ApiKeys]
    (
        [KeyId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Scopes] nvarchar(400) NOT NULL,
        [ExpiresUtc] datetime2 NULL,
        [RevokedUtc] datetime2 NULL,
        [Hash] nvarchar(128) NOT NULL,
        [Prefix] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([KeyId]),
        CONSTRAINT [FK_ApiKeys_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ApiKeys_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
    );
END;
GO

/* Other persisted feature tables */
IF OBJECT_ID(N'[dbo].[AiInsights]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AiInsights]
    (
        [InsightId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [JsonPayload] nvarchar(max) NOT NULL,
        [ModelId] nvarchar(120) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_AiInsights_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AiInsights] PRIMARY KEY ([InsightId])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[BaselineRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BaselineRuns]
    (
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [SetUtc] datetime2 NOT NULL CONSTRAINT [DF_BaselineRuns_SetUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_BaselineRuns] PRIMARY KEY ([ProjectId], [OperationId]),
        CONSTRAINT [FK_BaselineRuns_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE,
        CONSTRAINT [FK_BaselineRuns_TestRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [dbo].[TestRuns]([RunId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Environments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Environments]
    (
        [EnvironmentId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OwnerKey] nvarchar(100) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [BaseUrl] nvarchar(2048) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Environments_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Environments_UpdatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Environments] PRIMARY KEY ([EnvironmentId]),
        CONSTRAINT [FK_Environments_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[GeneratedDocs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GeneratedDocs]
    (
        [DocsId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SpecId] uniqueidentifier NOT NULL,
        [DocsJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_GeneratedDocs_CreatedUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_GeneratedDocs_UpdatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_GeneratedDocs] PRIMARY KEY ([DocsId]),
        CONSTRAINT [FK_GeneratedDocs_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_GeneratedDocs_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Subscriptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Subscriptions]
    (
        [OrganisationId] uniqueidentifier NOT NULL,
        [Plan] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Renews] bit NOT NULL CONSTRAINT [DF_Subscriptions_Renews] DEFAULT (1),
        [PeriodStartUtc] datetime2 NOT NULL,
        [PeriodEndUtc] datetime2 NOT NULL,
        [ProjectsUsed] int NOT NULL CONSTRAINT [DF_Subscriptions_ProjectsUsed] DEFAULT (0),
        [RunsUsed] int NOT NULL CONSTRAINT [DF_Subscriptions_RunsUsed] DEFAULT (0),
        [AiCallsUsed] int NOT NULL CONSTRAINT [DF_Subscriptions_AiCallsUsed] DEFAULT (0),
        [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_Subscriptions_UpdatedUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([OrganisationId]),
        CONSTRAINT [FK_Subscriptions_Organisations_OrganisationId]
            FOREIGN KEY ([OrganisationId]) REFERENCES [dbo].[Organisations]([OrganisationId]) ON DELETE CASCADE
    );
END;
GO

/* Indexes (idempotent) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND name = N'IX_Projects_Name')
    CREATE INDEX [IX_Projects_Name] ON [dbo].[Projects] ([Name]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND name = N'IX_Projects_OrganisationId')
    CREATE INDEX [IX_Projects_OrganisationId] ON [dbo].[Projects] ([OrganisationId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND name = N'IX_Projects_OwnerKey')
    CREATE INDEX [IX_Projects_OwnerKey] ON [dbo].[Projects] ([OwnerKey]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND name = N'IX_Projects_TenantId')
    CREATE INDEX [IX_Projects_TenantId] ON [dbo].[Projects] ([TenantId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND name = N'IX_Projects_TenantId_ProjectKey')
    CREATE UNIQUE INDEX [IX_Projects_TenantId_ProjectKey] ON [dbo].[Projects] ([TenantId], [ProjectKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_OrganisationId')
    CREATE INDEX [IX_TestRuns_OrganisationId] ON [dbo].[TestRuns] ([OrganisationId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_TenantId')
    CREATE INDEX [IX_TestRuns_TenantId] ON [dbo].[TestRuns] ([TenantId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_BaselineRunId')
    CREATE INDEX [IX_TestRuns_BaselineRunId] ON [dbo].[TestRuns] ([BaselineRunId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_SpecId')
    CREATE INDEX [IX_TestRuns_SpecId] ON [dbo].[TestRuns] ([SpecId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_ProjectId_StartedUtc')
    CREATE INDEX [IX_TestRuns_ProjectId_StartedUtc] ON [dbo].[TestRuns] ([ProjectId], [StartedUtc]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_StartedUtc_Desc')
    CREATE INDEX [IX_TestRuns_StartedUtc_Desc] ON [dbo].[TestRuns] ([StartedUtc] DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_ProjectId_StartedUtc_Desc')
    CREATE INDEX [IX_TestRuns_ProjectId_StartedUtc_Desc] ON [dbo].[TestRuns] ([ProjectId], [StartedUtc] DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestRuns]') AND name = N'IX_TestRuns_OperationId')
    CREATE INDEX [IX_TestRuns_OperationId] ON [dbo].[TestRuns] ([OperationId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestCaseResults]') AND name = N'IX_TestCaseResults_RunId')
    CREATE INDEX [IX_TestCaseResults_RunId] ON [dbo].[TestCaseResults] ([RunId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestCaseResults]') AND name = N'IX_TestCaseResults_RunId_Pass_Blocked')
    CREATE INDEX [IX_TestCaseResults_RunId_Pass_Blocked] ON [dbo].[TestCaseResults] ([RunId], [Pass], [Blocked]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[OpenApiSpecs]') AND name = N'IX_OpenApiSpecs_ProjectId')
    CREATE INDEX [IX_OpenApiSpecs_ProjectId] ON [dbo].[OpenApiSpecs] ([ProjectId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[OpenApiSpecs]') AND name = N'IX_OpenApiSpecs_TenantId')
    CREATE INDEX [IX_OpenApiSpecs_TenantId] ON [dbo].[OpenApiSpecs] ([TenantId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[OpenApiSpecs]') AND name = N'IX_OpenApiSpecs_ProjectId_SpecHash')
    CREATE UNIQUE INDEX [IX_OpenApiSpecs_ProjectId_SpecHash] ON [dbo].[OpenApiSpecs] ([ProjectId], [SpecHash]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Organisations]') AND name = N'IX_Organisations_Slug')
    CREATE UNIQUE INDEX [IX_Organisations_Slug] ON [dbo].[Organisations] ([Slug]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = N'IX_Users_ExternalId')
    CREATE UNIQUE INDEX [IX_Users_ExternalId] ON [dbo].[Users] ([ExternalId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Memberships]') AND name = N'IX_Memberships_UserId')
    CREATE INDEX [IX_Memberships_UserId] ON [dbo].[Memberships] ([UserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[RunAnnotations]') AND name = N'IX_RunAnnotations_RunId')
    CREATE INDEX [IX_RunAnnotations_RunId] ON [dbo].[RunAnnotations] ([RunId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[RunAnnotations]') AND name = N'IX_RunAnnotations_OwnerKey_RunId')
    CREATE INDEX [IX_RunAnnotations_OwnerKey_RunId] ON [dbo].[RunAnnotations] ([OwnerKey], [RunId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AuditEvents]') AND name = N'IX_AuditEvents_OrganisationId_CreatedUtc')
    CREATE INDEX [IX_AuditEvents_OrganisationId_CreatedUtc] ON [dbo].[AuditEvents] ([OrganisationId], [CreatedUtc]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AuditEvents]') AND name = N'IX_AuditEvents_OrganisationId_Action_CreatedUtc')
    CREATE INDEX [IX_AuditEvents_OrganisationId_Action_CreatedUtc] ON [dbo].[AuditEvents] ([OrganisationId], [Action], [CreatedUtc]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AiInsights]') AND name = N'IX_AiInsights_OrganisationId_OperationId')
    CREATE INDEX [IX_AiInsights_OrganisationId_OperationId] ON [dbo].[AiInsights] ([OrganisationId], [OperationId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AiInsights]') AND name = N'IX_AiInsights_OrganisationId_ProjectId_RunId')
    CREATE INDEX [IX_AiInsights_OrganisationId_ProjectId_RunId] ON [dbo].[AiInsights] ([OrganisationId], [ProjectId], [RunId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ApiKeys]') AND name = N'IX_ApiKeys_OrganisationId')
    CREATE INDEX [IX_ApiKeys_OrganisationId] ON [dbo].[ApiKeys] ([OrganisationId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ApiKeys]') AND name = N'IX_ApiKeys_Prefix')
    CREATE UNIQUE INDEX [IX_ApiKeys_Prefix] ON [dbo].[ApiKeys] ([Prefix]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ApiKeys]') AND name = N'IX_ApiKeys_UserId')
    CREATE INDEX [IX_ApiKeys_UserId] ON [dbo].[ApiKeys] ([UserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[BaselineRuns]') AND name = N'IX_BaselineRuns_RunId')
    CREATE INDEX [IX_BaselineRuns_RunId] ON [dbo].[BaselineRuns] ([RunId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Environments]') AND name = N'IX_Environments_ProjectId')
    CREATE INDEX [IX_Environments_ProjectId] ON [dbo].[Environments] ([ProjectId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Environments]') AND name = N'IX_Environments_OwnerKey_ProjectId_Name')
    CREATE UNIQUE INDEX [IX_Environments_OwnerKey_ProjectId_Name] ON [dbo].[Environments] ([OwnerKey], [ProjectId], [Name]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[GeneratedDocs]') AND name = N'IX_GeneratedDocs_OrganisationId')
    CREATE INDEX [IX_GeneratedDocs_OrganisationId] ON [dbo].[GeneratedDocs] ([OrganisationId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[GeneratedDocs]') AND name = N'IX_GeneratedDocs_ProjectId')
    CREATE INDEX [IX_GeneratedDocs_ProjectId] ON [dbo].[GeneratedDocs] ([ProjectId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[GeneratedDocs]') AND name = N'IX_GeneratedDocs_OrganisationId_ProjectId')
    CREATE UNIQUE INDEX [IX_GeneratedDocs_OrganisationId_ProjectId] ON [dbo].[GeneratedDocs] ([OrganisationId], [ProjectId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestPlans]') AND name = N'IX_TestPlans_ProjectId')
    CREATE INDEX [IX_TestPlans_ProjectId] ON [dbo].[TestPlans] ([ProjectId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TestPlanDrafts]') AND name = N'IX_TestPlanDrafts_ProjectId')
    CREATE INDEX [IX_TestPlanDrafts_ProjectId] ON [dbo].[TestPlanDrafts] ([ProjectId]);
GO

/* Verification (non-destructive) */
SELECT [name]
FROM sys.tables
WHERE [name] IN (
    N'Projects', N'TestRuns', N'TestCaseResults', N'OpenApiSpecs', N'TestPlans', N'TestPlanDrafts',
    N'RunAnnotations', N'Organisations', N'Users', N'Memberships', N'AuditEvents', N'AiInsights',
    N'ApiKeys', N'BaselineRuns', N'Environments', N'GeneratedDocs', N'Subscriptions'
)
ORDER BY [name];
GO

SELECT TOP (1) * FROM [dbo].[Projects];
SELECT TOP (1) * FROM [dbo].[TestRuns];
SELECT TOP (1) * FROM [dbo].[TestCaseResults];
GO
