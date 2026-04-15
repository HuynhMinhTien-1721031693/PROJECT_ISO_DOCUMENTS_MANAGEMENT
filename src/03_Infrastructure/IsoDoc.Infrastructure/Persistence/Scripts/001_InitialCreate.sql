/*
  ISO DMS V3 — initial relational schema (SQL Server), aligned with IsoDocDbContext / Fluent configurations.

  Apply once to your database (e.g. SSMS / sqlcmd), or use EF Core migrations when the
  ASP.NET Core 8.0 runtime + dotnet-ef are available on the machine:

  dotnet tool install -g dotnet-ef --version 8.0.11
  dotnet ef migrations add InitialCreate ^
    --project src/03_Infrastructure/IsoDoc.Infrastructure/IsoDoc.Infrastructure.csproj ^
    --startup-project src/04_WebAPI/IsoDoc.WebAPI/IsoDoc.WebAPI.csproj ^
    --output-dir Persistence/Migrations
*/

IF OBJECT_ID(N'[dbo].[ApprovalSteps]', N'U') IS NOT NULL DROP TABLE [dbo].[ApprovalSteps];
IF OBJECT_ID(N'[dbo].[ApprovalWorkflows]', N'U') IS NOT NULL DROP TABLE [dbo].[ApprovalWorkflows];
IF OBJECT_ID(N'[dbo].[DocumentVersions]', N'U') IS NOT NULL DROP TABLE [dbo].[DocumentVersions];
IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NOT NULL DROP TABLE [dbo].[AuditLogs];
IF OBJECT_ID(N'[dbo].[Documents]', N'U') IS NOT NULL DROP TABLE [dbo].[Documents];
GO

CREATE TABLE [dbo].[Documents] (
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Documents] PRIMARY KEY,
    [Title] NVARCHAR(500) NOT NULL,
    [DocumentCode] NVARCHAR(50) NOT NULL,
    [Standard] INT NOT NULL,
    [Category] INT NOT NULL,
    [Status] INT NOT NULL,
    [CurrentVersion] NVARCHAR(20) NOT NULL,
    [OwnerId] UNIQUEIDENTIFIER NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    [Description] NVARCHAR(4000) NULL,
    [Tags] NVARCHAR(4000) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [CreatedBy] UNIQUEIDENTIFIER NULL,
    [UpdatedAt] DATETIME2 NOT NULL,
    [UpdatedBy] UNIQUEIDENTIFIER NULL,
    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_Documents_IsDeleted] DEFAULT (0),
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] UNIQUEIDENTIFIER NULL
);
GO

CREATE UNIQUE INDEX [UX_Documents_DocumentCode]
    ON [dbo].[Documents]([DocumentCode])
    WHERE [IsDeleted] = 0;
GO

CREATE TABLE [dbo].[DocumentVersions] (
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_DocumentVersions] PRIMARY KEY,
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [BlobPath] NVARCHAR(1000) NOT NULL,
    [FileSize] BIGINT NOT NULL,
    [FileType] INT NOT NULL,
    [Checksum] NVARCHAR(64) NOT NULL,
    [ChangeNote] NVARCHAR(2000) NULL,
    [UploadedBy] UNIQUEIDENTIFIER NOT NULL,
    [UploadedAt] DATETIME2 NOT NULL,
    [IsCurrentVersion] BIT NOT NULL CONSTRAINT [DF_DocumentVersions_IsCurrent] DEFAULT (0),
    CONSTRAINT [FK_DocumentVersions_Documents] FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[ApprovalWorkflows] (
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ApprovalWorkflows] PRIMARY KEY,
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [CurrentStepOrder] INT NOT NULL,
    [Status] INT NOT NULL,
    [StartedAt] DATETIME2 NOT NULL,
    [CompletedAt] DATETIME2 NULL,
    CONSTRAINT [FK_ApprovalWorkflows_Documents] FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]),
    CONSTRAINT [FK_ApprovalWorkflows_DocumentVersions] FOREIGN KEY ([VersionId]) REFERENCES [dbo].[DocumentVersions]([Id])
);
GO

CREATE TABLE [dbo].[ApprovalSteps] (
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ApprovalSteps] PRIMARY KEY,
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [StepOrder] INT NOT NULL,
    [ApproverId] UNIQUEIDENTIFIER NOT NULL,
    [Decision] INT NOT NULL,
    [Comment] NVARCHAR(2000) NULL,
    [DecidedAt] DATETIME2 NULL,
    CONSTRAINT [FK_ApprovalSteps_ApprovalWorkflows] FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[ApprovalWorkflows]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[AuditLogs] (
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AuditLogs] PRIMARY KEY,
    [UserId] UNIQUEIDENTIFIER NULL,
    [Action] NVARCHAR(200) NOT NULL,
    [EntityType] NVARCHAR(200) NOT NULL,
    [EntityId] NVARCHAR(64) NOT NULL,
    [OldValues] NVARCHAR(MAX) NULL,
    [NewValues] NVARCHAR(MAX) NULL,
    [IpAddress] NVARCHAR(64) NULL,
    [TimestampUtc] DATETIME2 NOT NULL
);
GO

CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [dbo].[AuditLogs]([TimestampUtc]);
CREATE INDEX [IX_AuditLogs_Entity] ON [dbo].[AuditLogs]([EntityType], [EntityId]);
GO
