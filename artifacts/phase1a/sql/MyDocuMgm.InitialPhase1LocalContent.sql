/*
  MyDocuMgm Phase 1 local migration script.
  Safety gate: stop before any DDL or transaction when the selected database is not MyDocuMgm.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'MyDocuMgm'
    THROW 51000, N'WRONG_TARGET_DATABASE: select MyDocuMgm before running this script.', 1;
GO

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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [DisplayName] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [Tags] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [NormalizedName] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [Contents] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [ShortSummary] nvarchar(500) NULL,
        [DetailContent] nvarchar(max) NULL,
        [Status] nvarchar(30) NOT NULL,
        [Visibility] nvarchar(30) NOT NULL,
        [IsFavorite] bit NOT NULL,
        [ExperienceStatus] nvarchar(30) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Contents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Contents_ExperienceStatus] CHECK ([ExperienceStatus] IN ('NONE','WANT_TO_TRY','TRIED')),
        CONSTRAINT [CK_Contents_Status] CHECK ([Status] IN ('INBOX','REVIEW_REQUIRED','READY','DRAFTED','PUBLISHED','ARCHIVED')),
        CONSTRAINT [CK_Contents_Visibility] CHECK ([Visibility] IN ('PRIVATE','PUBLIC_ALLOWED')),
        CONSTRAINT [FK_Contents_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [CleaningLaundryDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Target] nvarchar(500) NULL,
        [Supplies] nvarchar(500) NULL,
        [Precautions] nvarchar(500) NULL,
        CONSTRAINT [PK_CleaningLaundryDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_CleaningLaundryDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [ContentSteps] (
        [Id] uniqueidentifier NOT NULL,
        [ContentId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NULL,
        CONSTRAINT [PK_ContentSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContentSteps_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [ContentTags] (
        [ContentId] uniqueidentifier NOT NULL,
        [TagId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ContentTags] PRIMARY KEY ([ContentId], [TagId]),
        CONSTRAINT [FK_ContentTags_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContentTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [CookingDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Servings] int NULL,
        [PreparationMinutes] int NULL,
        [CookingMinutes] int NULL,
        [Difficulty] nvarchar(500) NULL,
        CONSTRAINT [PK_CookingDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_CookingDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [ExerciseDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [TargetArea] nvarchar(500) NULL,
        [DurationMinutes] int NULL,
        [Difficulty] nvarchar(500) NULL,
        [Equipment] nvarchar(500) NULL,
        CONSTRAINT [PK_ExerciseDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_ExerciseDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [MediaAssets] (
        [Id] uniqueidentifier NOT NULL,
        [ContentId] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(100) NOT NULL,
        [RelativePath] nvarchar(500) NOT NULL,
        [MimeType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [Width] int NOT NULL,
        [Height] int NOT NULL,
        [SortOrder] int NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsPublicAllowed] bit NOT NULL,
        [StorageStatus] nvarchar(20) NOT NULL,
        [FailureReason] nvarchar(500) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MediaAssets_StorageStatus] CHECK ([StorageStatus] IN ('PENDING','READY','FAILED')),
        CONSTRAINT [FK_MediaAssets_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [OtherDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [CustomLabel] nvarchar(500) NULL,
        [AdditionalInfo] nvarchar(4000) NULL,
        CONSTRAINT [PK_OtherDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_OtherDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [PhoneComputerDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [DeviceOrOs] nvarchar(500) NULL,
        [AppOrProgram] nvarchar(500) NULL,
        [Problem] nvarchar(500) NULL,
        [Solution] nvarchar(4000) NULL,
        CONSTRAINT [PK_PhoneComputerDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_PhoneComputerDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [PhotoDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Camera] nvarchar(500) NULL,
        [Lens] nvarchar(500) NULL,
        [ShootingSettings] nvarchar(500) NULL,
        [Location] nvarchar(500) NULL,
        CONSTRAINT [PK_PhotoDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_PhotoDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [PlaceDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Address] nvarchar(500) NULL,
        [BusinessHours] nvarchar(500) NULL,
        [ParkingInfo] nvarchar(500) NULL,
        [RecommendedMenuOrSpot] nvarchar(500) NULL,
        CONSTRAINT [PK_PlaceDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_PlaceDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [ProductDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Brand] nvarchar(500) NULL,
        [ModelName] nvarchar(500) NULL,
        [Price] decimal(18,2) NULL,
        [PurchasePlace] nvarchar(500) NULL,
        CONSTRAINT [PK_ProductDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_ProductDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [SourceEvidence] (
        [Id] uniqueidentifier NOT NULL,
        [ContentId] uniqueidentifier NOT NULL,
        [SourceType] nvarchar(40) NOT NULL,
        [SourceTitle] nvarchar(300) NULL,
        [SourceReference] nvarchar(2000) NULL,
        [CapturedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SourceEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SourceEvidence_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [StudyDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Subject] nvarchar(500) NULL,
        [LearningGoal] nvarchar(500) NULL,
        [Resource] nvarchar(500) NULL,
        [ReviewCycle] nvarchar(500) NULL,
        CONSTRAINT [PK_StudyDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_StudyDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [TipDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Situation] nvarchar(500) NULL,
        [KeyPoint] nvarchar(500) NULL,
        [Precautions] nvarchar(500) NULL,
        CONSTRAINT [PK_TipDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_TipDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [TravelDetails] (
        [ContentId] uniqueidentifier NOT NULL,
        [Destination] nvarchar(500) NULL,
        [BestSeason] nvarchar(500) NULL,
        [Transportation] nvarchar(500) NULL,
        [BudgetNote] nvarchar(500) NULL,
        CONSTRAINT [PK_TravelDetails] PRIMARY KEY ([ContentId]),
        CONSTRAINT [FK_TravelDetails_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE TABLE [CookingIngredients] (
        [Id] uniqueidentifier NOT NULL,
        [ContentId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Quantity] nvarchar(100) NULL,
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_CookingIngredients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CookingIngredients_CookingDetails_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [CookingDetails] ([ContentId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [Code], [DisplayName], [SortOrder])
    VALUES (''10000000-0000-0000-0000-000000000001'', N''PLACE'', N''가볼곳'', 1),
    (''10000000-0000-0000-0000-000000000002'', N''COOKING'', N''요리'', 2),
    (''10000000-0000-0000-0000-000000000003'', N''EXERCISE'', N''운동'', 3),
    (''10000000-0000-0000-0000-000000000004'', N''CLEANING_LAUNDRY'', N''청소&세탁'', 4),
    (''10000000-0000-0000-0000-000000000005'', N''TRAVEL'', N''여행'', 5),
    (''10000000-0000-0000-0000-000000000006'', N''PHOTO'', N''사진'', 6),
    (''10000000-0000-0000-0000-000000000007'', N''STUDY'', N''공부'', 7),
    (''10000000-0000-0000-0000-000000000008'', N''PRODUCT'', N''제품'', 8),
    (''10000000-0000-0000-0000-000000000009'', N''PHONE_COMPUTER'', N''폰·컴퓨터'', 9),
    (''10000000-0000-0000-0000-000000000010'', N''TIP'', N''팁'', 10),
    (''10000000-0000-0000-0000-000000000011'', N''OTHER'', N''기타'', 11)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Code] ON [Categories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_SortOrder] ON [Categories] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_Contents_CategoryId_Status_IsFavorite] ON [Contents] ([CategoryId], [Status], [IsFavorite]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_Contents_IsDeleted_UpdatedAtUtc] ON [Contents] ([IsDeleted], [UpdatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_Contents_Title] ON [Contents] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContentSteps_ContentId_SortOrder] ON [ContentSteps] ([ContentId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_ContentTags_TagId] ON [ContentTags] ([TagId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CookingIngredients_ContentId_SortOrder] ON [CookingIngredients] ([ContentId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_MediaAssets_ContentId_SortOrder] ON [MediaAssets] ([ContentId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MediaAssets_RelativePath] ON [MediaAssets] ([RelativePath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_MediaAssets_Sha256] ON [MediaAssets] ([Sha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE INDEX [IX_SourceEvidence_ContentId] ON [SourceEvidence] ([ContentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tags_NormalizedName] ON [Tags] ([NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729074010_InitialPhase1LocalContent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729074010_InitialPhase1LocalContent', N'10.0.10');
END;

COMMIT;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [MediaAssets] ADD [IsSelected] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [MediaAssets] ADD [SourceTimestampMs] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [CookingIngredients] ADD [IngredientType] nvarchar(30) NOT NULL DEFAULT N'부재료';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [CookingIngredients] ADD [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [CookingIngredients] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [ContentSteps] ADD [MediaAssetId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [Contents] ADD [CurrentWorkflowStep] nvarchar(30) NOT NULL DEFAULT N'URL';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [Categories] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [Categories] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE TABLE [CategorySearchAttributes] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [AttributeKey] nvarchar(80) NOT NULL,
        [DisplayName] nvarchar(80) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSearchable] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CategorySearchAttributes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategorySearchAttributes_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000005'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000006'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000007'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000008'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [DisplayName] = N''폰&컴'', [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000009'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000010'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'UPDATE [Categories] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = ''10000000-0000-0000-0000-000000000011'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AttributeKey', N'CategoryId', N'DisplayName', N'IsActive', N'IsSearchable', N'SortOrder') AND [object_id] = OBJECT_ID(N'[CategorySearchAttributes]'))
        SET IDENTITY_INSERT [CategorySearchAttributes] ON;
    EXEC(N'INSERT INTO [CategorySearchAttributes] ([Id], [AttributeKey], [CategoryId], [DisplayName], [IsActive], [IsSearchable], [SortOrder])
    VALUES (''20000000-0000-0000-0001-000000000001'', N''region'', ''10000000-0000-0000-0000-000000000001'', N''지역'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0001-000000000002'', N''parking'', ''10000000-0000-0000-0000-000000000001'', N''주차'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0002-000000000001'', N''primaryIngredient'', ''10000000-0000-0000-0000-000000000002'', N''주재료'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0002-000000000002'', N''difficulty'', ''10000000-0000-0000-0000-000000000002'', N''난이도'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0002-000000000003'', N''time'', ''10000000-0000-0000-0000-000000000002'', N''소요시간'', CAST(1 AS bit), CAST(1 AS bit), 3),
    (''20000000-0000-0000-0003-000000000001'', N''targetArea'', ''10000000-0000-0000-0000-000000000003'', N''운동 부위'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0003-000000000002'', N''equipment'', ''10000000-0000-0000-0000-000000000003'', N''준비물'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0004-000000000001'', N''target'', ''10000000-0000-0000-0000-000000000004'', N''대상'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0004-000000000002'', N''supplies'', ''10000000-0000-0000-0000-000000000004'', N''세제·제품'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0005-000000000001'', N''destination'', ''10000000-0000-0000-0000-000000000005'', N''국가·지역'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0005-000000000002'', N''transport'', ''10000000-0000-0000-0000-000000000005'', N''교통'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0006-000000000001'', N''camera'', ''10000000-0000-0000-0000-000000000006'', N''기기'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0006-000000000002'', N''location'', ''10000000-0000-0000-0000-000000000006'', N''촬영 장소'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0007-000000000001'', N''subject'', ''10000000-0000-0000-0000-000000000007'', N''분야'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0007-000000000002'', N''resource'', ''10000000-0000-0000-0000-000000000007'', N''참고 자료'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0008-000000000001'', N''brand'', ''10000000-0000-0000-0000-000000000008'', N''브랜드'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0008-000000000002'', N''store'', ''10000000-0000-0000-0000-000000000008'', N''구매처'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0008-000000000003'', N''price'', ''10000000-0000-0000-0000-000000000008'', N''가격대'', CAST(1 AS bit), CAST(1 AS bit), 3),
    (''20000000-0000-0000-0009-000000000001'', N''deviceOrOs'', ''10000000-0000-0000-0000-000000000009'', N''기기·OS'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0009-000000000002'', N''problem'', ''10000000-0000-0000-0000-000000000009'', N''문제 유형'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0010-000000000001'', N''situation'', ''10000000-0000-0000-0000-000000000010'', N''적용 상황'', CAST(1 AS bit), CAST(1 AS bit), 1),
    (''20000000-0000-0000-0010-000000000002'', N''keyPoint'', ''10000000-0000-0000-0000-000000000010'', N''주제'', CAST(1 AS bit), CAST(1 AS bit), 2),
    (''20000000-0000-0000-0011-000000000001'', N''customLabel'', ''10000000-0000-0000-0000-000000000011'', N''주제'', CAST(1 AS bit), CAST(1 AS bit), 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AttributeKey', N'CategoryId', N'DisplayName', N'IsActive', N'IsSearchable', N'SortOrder') AND [object_id] = OBJECT_ID(N'[CategorySearchAttributes]'))
        SET IDENTITY_INSERT [CategorySearchAttributes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE INDEX [IX_MediaAssets_ContentId_IsSelected_SourceTimestampMs] ON [MediaAssets] ([ContentId], [IsSelected], [SourceTimestampMs]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE INDEX [IX_MediaAssets_ContentId_Sha256] ON [MediaAssets] ([ContentId], [Sha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE INDEX [IX_ContentSteps_MediaAssetId] ON [ContentSteps] ([MediaAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE INDEX [IX_Contents_CurrentWorkflowStep_UpdatedAtUtc] ON [Contents] ([CurrentWorkflowStep], [UpdatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    EXEC(N'ALTER TABLE [Contents] ADD CONSTRAINT [CK_Contents_CurrentWorkflowStep] CHECK ([CurrentWorkflowStep] IN (''URL'',''ANALYSIS_REVIEW'',''CATEGORY_EDIT'',''MEDIA'',''DETAIL'',''BLOG_DRAFT'',''COMPLETED''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CategorySearchAttributes_CategoryId_AttributeKey] ON [CategorySearchAttributes] ([CategoryId], [AttributeKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    CREATE INDEX [IX_CategorySearchAttributes_CategoryId_IsActive_IsSearchable_SortOrder] ON [CategorySearchAttributes] ([CategoryId], [IsActive], [IsSearchable], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    ALTER TABLE [ContentSteps] ADD CONSTRAINT [FK_ContentSteps_MediaAssets_MediaAssetId] FOREIGN KEY ([MediaAssetId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729144229_AlignWireframeV7Schema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729144229_AlignWireframeV7Schema', N'10.0.10');
END;

COMMIT;
GO
