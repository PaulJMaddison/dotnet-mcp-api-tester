IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE TABLE [Projects] (
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([ProjectId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE TABLE [TestRuns] (
        [RunId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [StartedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NOT NULL,
        [TotalCases] int NOT NULL,
        [Passed] int NOT NULL,
        [Failed] int NOT NULL,
        [Blocked] int NOT NULL,
        [TotalDurationMs] bigint NOT NULL,
        CONSTRAINT [PK_TestRuns] PRIMARY KEY ([RunId]),
        CONSTRAINT [FK_TestRuns_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE TABLE [TestCaseResults] (
        [TestCaseResultId] bigint NOT NULL IDENTITY,
        [RunId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Blocked] bit NOT NULL,
        [BlockReason] nvarchar(max) NULL,
        [Method] nvarchar(16) NOT NULL,
        [Url] nvarchar(max) NULL,
        [StatusCode] int NULL,
        [DurationMs] bigint NOT NULL,
        [Pass] bit NOT NULL,
        [FailureReason] nvarchar(max) NULL,
        [ResponseSnippet] nvarchar(max) NULL,
        CONSTRAINT [PK_TestCaseResults] PRIMARY KEY ([TestCaseResultId]),
        CONSTRAINT [FK_TestCaseResults_TestRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [TestRuns] ([RunId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Projects_Name] ON [Projects] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_TestCaseResults_RunId] ON [TestCaseResults] ([RunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_TestRuns_ProjectId_StartedUtc] ON [TestRuns] ([ProjectId], [StartedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228192911_Day14_InitialPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251228192911_Day14_InitialPersistence', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230144523_Day15_ProjectKey'
)
BEGIN
    ALTER TABLE [Projects] ADD [ProjectKey] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230144523_Day15_ProjectKey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_ProjectKey] ON [Projects] ([ProjectKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230144523_Day15_ProjectKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251230144523_Day15_ProjectKey', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105090000_Day20_OpenApiSpecs'
)
BEGIN
    CREATE TABLE [OpenApiSpecs] (
        [SpecId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Version] nvarchar(50) NOT NULL,
        [SpecJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OpenApiSpecs] PRIMARY KEY ([SpecId]),
        CONSTRAINT [FK_OpenApiSpecs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105090000_Day20_OpenApiSpecs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpenApiSpecs_ProjectId] ON [OpenApiSpecs] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105090000_Day20_OpenApiSpecs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105090000_Day20_OpenApiSpecs', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260112090000_Day21_TestPlans'
)
BEGIN
    CREATE TABLE [TestPlans] (
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [PlanJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TestPlans] PRIMARY KEY ([ProjectId], [OperationId]),
        CONSTRAINT [FK_TestPlans_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260112090000_Day21_TestPlans'
)
BEGIN
    CREATE INDEX [IX_TestPlans_ProjectId] ON [TestPlans] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260112090000_Day21_TestPlans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260112090000_Day21_TestPlans', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119090000_Day23_OwnerKey'
)
BEGIN
    DROP INDEX [IX_Projects_ProjectKey] ON [Projects];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119090000_Day23_OwnerKey'
)
BEGIN
    ALTER TABLE [Projects] ADD [OwnerKey] nvarchar(100) NOT NULL DEFAULT N'default';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119090000_Day23_OwnerKey'
)
BEGIN
    CREATE INDEX [IX_Projects_OwnerKey] ON [Projects] ([OwnerKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119090000_Day23_OwnerKey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_OwnerKey_ProjectKey] ON [Projects] ([OwnerKey], [ProjectKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119090000_Day23_OwnerKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260119090000_Day23_OwnerKey', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219090000_Day41_BaselineRuns'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [BaselineRunId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219090000_Day41_BaselineRuns'
)
BEGIN
    CREATE INDEX [IX_TestRuns_BaselineRunId] ON [TestRuns] ([BaselineRunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219090000_Day41_BaselineRuns'
)
BEGIN
    ALTER TABLE [TestRuns] ADD CONSTRAINT [FK_TestRuns_TestRuns_BaselineRunId] FOREIGN KEY ([BaselineRunId]) REFERENCES [TestRuns] ([RunId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219090000_Day41_BaselineRuns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260219090000_Day41_BaselineRuns', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226090000_Day42_ResultClassification'
)
BEGIN
    ALTER TABLE [TestCaseResults] ADD [Classification] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260226090000_Day42_ResultClassification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260226090000_Day42_ResultClassification', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    DROP INDEX [IX_OpenApiSpecs_ProjectId] ON [OpenApiSpecs];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [SpecId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    ALTER TABLE [OpenApiSpecs] ADD [SpecHash] nvarchar(64) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    CREATE INDEX [IX_TestRuns_SpecId] ON [TestRuns] ([SpecId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    CREATE INDEX [IX_OpenApiSpecs_ProjectId] ON [OpenApiSpecs] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpenApiSpecs_ProjectId_SpecHash] ON [OpenApiSpecs] ([ProjectId], [SpecHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    ALTER TABLE [TestRuns] ADD CONSTRAINT [FK_TestRuns_OpenApiSpecs_SpecId] FOREIGN KEY ([SpecId]) REFERENCES [OpenApiSpecs] ([SpecId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305090000_Day43_SpecHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305090000_Day43_SpecHistory', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312090000_Day47_AuditTrail'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [Actor] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312090000_Day47_AuditTrail'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [EnvironmentBaseUrl] nvarchar(2048) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312090000_Day47_AuditTrail'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [EnvironmentName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312090000_Day47_AuditTrail'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [PolicySnapshotJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312090000_Day47_AuditTrail'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260312090000_Day47_AuditTrail', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260319090000_Day51_RunAnnotations'
)
BEGIN
    CREATE TABLE [RunAnnotations] (
        [AnnotationId] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [OwnerKey] nvarchar(100) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [JiraLink] nvarchar(2048) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RunAnnotations] PRIMARY KEY ([AnnotationId]),
        CONSTRAINT [FK_RunAnnotations_TestRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [TestRuns] ([RunId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260319090000_Day51_RunAnnotations'
)
BEGIN
    CREATE INDEX [IX_RunAnnotations_RunId] ON [RunAnnotations] ([RunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260319090000_Day51_RunAnnotations'
)
BEGIN
    CREATE INDEX [IX_RunAnnotations_OwnerKey_RunId] ON [RunAnnotations] ([OwnerKey], [RunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260319090000_Day51_RunAnnotations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260319090000_Day51_RunAnnotations', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE TABLE [Organisations] (
        [OrganisationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Organisations] PRIMARY KEY ([OrganisationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] uniqueidentifier NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE TABLE [Memberships] (
        [OrganisationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Memberships] PRIMARY KEY ([OrganisationId], [UserId]),
        CONSTRAINT [FK_Memberships_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Memberships_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organisations_Slug] ON [Organisations] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_ExternalId] ON [Users] ([ExternalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE INDEX [IX_Memberships_UserId] ON [Memberships] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'OrganisationId', N'Name', N'Slug', N'CreatedUtc') AND [object_id] = OBJECT_ID(N'[Organisations]'))
        SET IDENTITY_INSERT [Organisations] ON;
    EXEC(N'INSERT INTO [Organisations] ([OrganisationId], [Name], [Slug], [CreatedUtc])
    VALUES (''11111111-1111-1111-1111-111111111111'', N''Local Dev'', N''local-dev'', ''2026-04-19T09:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'OrganisationId', N'Name', N'Slug', N'CreatedUtc') AND [object_id] = OBJECT_ID(N'[Organisations]'))
        SET IDENTITY_INSERT [Organisations] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    ALTER TABLE [Projects] ADD [OrganisationId] uniqueidentifier NOT NULL DEFAULT '11111111-1111-1111-1111-111111111111';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    ALTER TABLE [TestRuns] ADD [OrganisationId] uniqueidentifier NOT NULL DEFAULT '11111111-1111-1111-1111-111111111111';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE INDEX [IX_Projects_OrganisationId] ON [Projects] ([OrganisationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    DROP INDEX [IX_Projects_OwnerKey_ProjectKey] ON [Projects];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_OrganisationId_ProjectKey] ON [Projects] ([OrganisationId], [ProjectKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    CREATE INDEX [IX_TestRuns_OrganisationId] ON [TestRuns] ([OrganisationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    ALTER TABLE [TestRuns] ADD CONSTRAINT [FK_TestRuns_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419090000_Day70_OrgUsersRoles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419090000_Day70_OrgUsersRoles', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421090000_Day72_AuditLog'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [AuditEventId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [TargetType] nvarchar(100) NOT NULL,
        [TargetId] nvarchar(200) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [MetadataJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([AuditEventId]),
        CONSTRAINT [FK_AuditEvents_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421090000_Day72_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OrganisationId_CreatedUtc] ON [AuditEvents] ([OrganisationId], [CreatedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421090000_Day72_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OrganisationId_Action_CreatedUtc] ON [AuditEvents] ([OrganisationId], [Action], [CreatedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421090000_Day72_AuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260421090000_Day72_AuditLog', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505090000_Day81_AiTestPlanDrafts'
)
BEGIN
    CREATE TABLE [TestPlanDrafts] (
        [DraftId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [PlanJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TestPlanDrafts] PRIMARY KEY ([DraftId]),
        CONSTRAINT [FK_TestPlanDrafts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505090000_Day81_AiTestPlanDrafts'
)
BEGIN
    CREATE INDEX [IX_TestPlanDrafts_ProjectId] ON [TestPlanDrafts] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505090000_Day81_AiTestPlanDrafts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505090000_Day81_AiTestPlanDrafts', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506090000_Day76_FlakeHandling'
)
BEGIN
    ALTER TABLE [TestCaseResults] ADD [FlakeReasonCategory] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506090000_Day76_FlakeHandling'
)
BEGIN
    ALTER TABLE [TestCaseResults] ADD [IsFlaky] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506090000_Day76_FlakeHandling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260506090000_Day76_FlakeHandling', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [OpenApiSpecs] DROP CONSTRAINT [FK_OpenApiSpecs_Projects_ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity_ProjectId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [Projects] DROP CONSTRAINT [AK_Projects_TempId_TempId1];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Projects]') AND [c].[name] = N'TempId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Projects] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Projects] DROP COLUMN [TempId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Projects]') AND [c].[name] = N'TempId1');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Projects] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Projects] DROP COLUMN [TempId1];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OpenApiSpecs]') AND [c].[name] = N'ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [OpenApiSpecs] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [OpenApiSpecs] DROP COLUMN [ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [Organisations] ADD [OrgSettingsJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [Organisations] ADD [RedactionRulesJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [Organisations] ADD [RetentionDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [AiInsights] (
        [InsightId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [JsonPayload] nvarchar(max) NOT NULL,
        [ModelId] nvarchar(120) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AiInsights] PRIMARY KEY ([InsightId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [ApiKeys] (
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
        CONSTRAINT [FK_ApiKeys_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ApiKeys_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [BaselineRuns] (
        [ProjectId] uniqueidentifier NOT NULL,
        [OperationId] nvarchar(200) NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [SetUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BaselineRuns] PRIMARY KEY ([ProjectId], [OperationId]),
        CONSTRAINT [FK_BaselineRuns_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE,
        CONSTRAINT [FK_BaselineRuns_TestRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [TestRuns] ([RunId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [Environments] (
        [EnvironmentId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OwnerKey] nvarchar(100) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [BaseUrl] nvarchar(2048) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Environments] PRIMARY KEY ([EnvironmentId]),
        CONSTRAINT [FK_Environments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [GeneratedDocs] (
        [DocsId] uniqueidentifier NOT NULL,
        [OrganisationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SpecId] uniqueidentifier NOT NULL,
        [DocsJson] nvarchar(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_GeneratedDocs] PRIMARY KEY ([DocsId]),
        CONSTRAINT [FK_GeneratedDocs_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_GeneratedDocs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [OrganisationId] uniqueidentifier NOT NULL,
        [Plan] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Renews] bit NOT NULL,
        [PeriodStartUtc] datetime2 NOT NULL,
        [PeriodEndUtc] datetime2 NOT NULL,
        [ProjectsUsed] int NOT NULL,
        [RunsUsed] int NOT NULL,
        [AiCallsUsed] int NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([OrganisationId]),
        CONSTRAINT [FK_Subscriptions_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([OrganisationId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_AiInsights_OrganisationId_OperationId] ON [AiInsights] ([OrganisationId], [OperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_AiInsights_OrganisationId_ProjectId_RunId] ON [AiInsights] ([OrganisationId], [ProjectId], [RunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_OrganisationId] ON [ApiKeys] ([OrganisationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ApiKeys_Prefix] ON [ApiKeys] ([Prefix]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_UserId] ON [ApiKeys] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_BaselineRuns_RunId] ON [BaselineRuns] ([RunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Environments_OwnerKey_ProjectId_Name] ON [Environments] ([OwnerKey], [ProjectId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_Environments_ProjectId] ON [Environments] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_GeneratedDocs_OrganisationId] ON [GeneratedDocs] ([OrganisationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GeneratedDocs_OrganisationId_ProjectId] ON [GeneratedDocs] ([OrganisationId], [ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    CREATE INDEX [IX_GeneratedDocs_ProjectId] ON [GeneratedDocs] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    ALTER TABLE [OpenApiSpecs] ADD CONSTRAINT [FK_OpenApiSpecs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616090000_Day107_DbSchemaSync'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260616090000_Day107_DbSchemaSync', N'8.0.22');
END;
GO

COMMIT;
GO

