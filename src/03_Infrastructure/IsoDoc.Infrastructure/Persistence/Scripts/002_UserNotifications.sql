-- In-app notifications (Phase 4). Run against IsoDocumentDb after initial schema.

IF OBJECT_ID(N'[dbo].[UserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserNotifications] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_UserNotifications] PRIMARY KEY,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(500) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [ActionUrl] NVARCHAR(2000) NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_UserNotifications_IsRead] DEFAULT (0),
        [CreatedAt] DATETIME2 NOT NULL,
        [CreatedBy] UNIQUEIDENTIFIER NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        [UpdatedBy] UNIQUEIDENTIFIER NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_UserNotifications_IsDeleted] DEFAULT (0),
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] UNIQUEIDENTIFIER NULL
    );

    CREATE NONCLUSTERED INDEX [IX_UserNotifications_User_Read]
        ON [dbo].[UserNotifications] ([UserId], [IsRead]);

    CREATE NONCLUSTERED INDEX [IX_UserNotifications_CreatedAt]
        ON [dbo].[UserNotifications] ([CreatedAt] DESC);
END
GO
