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
