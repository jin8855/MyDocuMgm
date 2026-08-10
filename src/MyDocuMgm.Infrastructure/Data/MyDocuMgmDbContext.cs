using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class MyDocuMgmDbContext(DbContextOptions<MyDocuMgmDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategorySearchAttribute> CategorySearchAttributes => Set<CategorySearchAttribute>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<BlogDraft> BlogDrafts => Set<BlogDraft>();
    public DbSet<ContentStep> ContentSteps => Set<ContentStep>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ContentMediaLink> ContentMediaLinks => Set<ContentMediaLink>();
    public DbSet<SourceEvidence> SourceEvidence => Set<SourceEvidence>();
    public DbSet<PlaceDetails> PlaceDetails => Set<PlaceDetails>();
    public DbSet<CookingDetails> CookingDetails => Set<CookingDetails>();
    public DbSet<CookingIngredient> CookingIngredients => Set<CookingIngredient>();
    public DbSet<ExerciseDetails> ExerciseDetails => Set<ExerciseDetails>();
    public DbSet<CleaningLaundryDetails> CleaningLaundryDetails => Set<CleaningLaundryDetails>();
    public DbSet<TravelDetails> TravelDetails => Set<TravelDetails>();
    public DbSet<PhotoDetails> PhotoDetails => Set<PhotoDetails>();
    public DbSet<StudyDetails> StudyDetails => Set<StudyDetails>();
    public DbSet<ProductDetails> ProductDetails => Set<ProductDetails>();
    public DbSet<PhoneComputerDetails> PhoneComputerDetails => Set<PhoneComputerDetails>();
    public DbSet<TipDetails> TipDetails => Set<TipDetails>();
    public DbSet<OtherDetails> OtherDetails => Set<OtherDetails>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTrackedDetails();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTrackedDetails();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCategory(modelBuilder);
        ConfigureContent(modelBuilder);
        ConfigureChildren(modelBuilder);
        ConfigureDetails(modelBuilder);
    }

    private static void ConfigureCategory(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();
        category.ToTable("Categories");
        category.HasKey(value => value.Id);
        category.Property(value => value.Code).HasMaxLength(40).IsRequired();
        category.Property(value => value.DisplayName).HasMaxLength(80).IsRequired();
        category.Property(value => value.RowVersion).IsRowVersion();
        category.HasIndex(value => value.Code).IsUnique();
        category.HasIndex(value => value.SortOrder).IsUnique();
        category.HasData(CategoryCatalog.All.Select(value => new Category
        {
            Id = value.Id,
            SortOrder = value.SortOrder,
            Code = value.Code,
            DisplayName = value.DisplayName,
            IsActive = true
        }));

        var attribute = modelBuilder.Entity<CategorySearchAttribute>();
        attribute.ToTable("CategorySearchAttributes");
        attribute.HasKey(value => value.Id);
        attribute.Property(value => value.AttributeKey).HasMaxLength(80).IsRequired();
        attribute.Property(value => value.DisplayName).HasMaxLength(80).IsRequired();
        attribute.Property(value => value.RowVersion).IsRowVersion();
        attribute.HasIndex(value => new { value.CategoryId, value.AttributeKey }).IsUnique();
        attribute.HasIndex(value => new { value.CategoryId, value.IsActive, value.IsSearchable, value.SortOrder });
        attribute.HasOne(value => value.Category)
            .WithMany(value => value.SearchAttributes)
            .HasForeignKey(value => value.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        attribute.HasData(CategorySearchAttributeCatalog.All.Select(value => new CategorySearchAttribute
        {
            Id = value.Id,
            CategoryId = value.CategoryId,
            AttributeKey = value.AttributeKey,
            DisplayName = value.DisplayName,
            SortOrder = value.SortOrder,
            IsActive = value.IsActive,
            IsSearchable = value.IsSearchable
        }));
    }

    private static void ConfigureContent(ModelBuilder modelBuilder)
    {
        var content = modelBuilder.Entity<Content>();
        content.ToTable("Contents", table =>
        {
            table.HasCheckConstraint("CK_Contents_Status", "[Status] IN ('INBOX','REVIEW_REQUIRED','READY','DRAFTED','PUBLISHED','ARCHIVED')");
            table.HasCheckConstraint("CK_Contents_Visibility", "[Visibility] IN ('PRIVATE','PUBLIC_ALLOWED')");
            table.HasCheckConstraint("CK_Contents_ExperienceStatus", "[ExperienceStatus] IN ('NONE','WANT_TO_TRY','TRIED')");
            table.HasCheckConstraint(
                "CK_Contents_CurrentWorkflowStep",
                "[CurrentWorkflowStep] IN ('URL','ANALYSIS_REVIEW','CATEGORY_EDIT','MEDIA','DETAIL','BLOG_DRAFT','COMPLETED')");
            table.HasCheckConstraint(
                "CK_Contents_SourceKind",
                "[SourceKind] IS NULL OR [SourceKind] IN ('GENERIC','INSTAGRAM')");
            table.HasCheckConstraint(
                "CK_Contents_IntakeStatus",
                "[IntakeStatus] IS NULL OR [IntakeStatus] IN ('URL_ACCEPTED','MANUAL_INPUT_REQUIRED','CONTENT_READY')");
            table.HasCheckConstraint(
                "CK_Contents_InstagramContentType",
                "[InstagramContentType] IS NULL OR [InstagramContentType] IN ('POST','REEL')");
            table.HasCheckConstraint(
                "CK_Contents_PinnedAuthorCommentState",
                "[PinnedAuthorCommentState] IS NULL OR [PinnedAuthorCommentState] IN ('PRESENT','NONE')");
            table.HasCheckConstraint(
                "CK_Contents_SourceAcquisitionMode",
                "[SourceAcquisitionMode] IS NULL OR [SourceAcquisitionMode] = 'MANUAL'");
            table.HasCheckConstraint(
                "CK_Contents_PinnedAuthorCommentConsistency",
                "([PinnedAuthorCommentState] IS NULL AND [PinnedAuthorCommentText] IS NULL) OR ([PinnedAuthorCommentState] = 'NONE' AND [PinnedAuthorCommentText] IS NULL) OR ([PinnedAuthorCommentState] = 'PRESENT' AND LEN(LTRIM(RTRIM([PinnedAuthorCommentText]))) > 0)");
        });
        content.HasKey(value => value.Id);
        content.Property(value => value.Title).HasMaxLength(200).IsRequired();
        content.Property(value => value.ShortSummary).HasMaxLength(500);
        content.Property(value => value.DetailContent).HasMaxLength(20_000);
        content.Property(value => value.Status).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.Visibility).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.ExperienceStatus).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.CurrentWorkflowStep).HasConversion<string>().HasMaxLength(30).HasDefaultValue(WorkflowStep.URL);
        content.Property(value => value.OriginalUrl).HasMaxLength(2048);
        content.Property(value => value.NormalizedUrl).HasMaxLength(2048);
        content.Property(value => value.NormalizedUrlHash).HasColumnType("binary(32)");
        content.Property(value => value.SourceKind).HasConversion<string>().HasMaxLength(20);
        content.Property(value => value.InstagramContentType).HasConversion<string>().HasMaxLength(20);
        content.Property(value => value.ManualCaption).HasMaxLength(Content.ManualCaptionMaxLength);
        content.Property(value => value.PinnedAuthorCommentState).HasConversion<string>().HasMaxLength(20);
        content.Property(value => value.PinnedAuthorCommentText).HasMaxLength(Content.PinnedAuthorCommentMaxLength);
        content.Property(value => value.SourceAcquisitionMode).HasConversion<string>().HasMaxLength(20);
        content.Property(value => value.IntakeStatus).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.CreatedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.UpdatedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.DeletedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.RowVersion).IsRowVersion();
        content.HasQueryFilter(value => !value.IsDeleted);
        content.HasOne(value => value.Category).WithMany(value => value.Contents).HasForeignKey(value => value.CategoryId).OnDelete(DeleteBehavior.Restrict);
        content.HasIndex(value => new { value.IsDeleted, value.UpdatedAtUtc });
        content.HasIndex(value => new { value.CategoryId, value.Status, value.IsFavorite });
        content.HasIndex(value => new { value.CurrentWorkflowStep, value.UpdatedAtUtc });
        content.HasIndex(value => value.Title);
        content.HasIndex(value => value.NormalizedUrlHash)
            .IsUnique()
            .HasFilter("[NormalizedUrlHash] IS NOT NULL");
    }

    private static void ConfigureChildren(ModelBuilder modelBuilder)
    {
        var blogDraft = modelBuilder.Entity<BlogDraft>();
        blogDraft.ToTable("BlogDrafts");
        blogDraft.HasKey(value => value.Id);
        blogDraft.Property(value => value.Title).HasMaxLength(BlogDraft.TitleMaxLength).IsRequired();
        blogDraft.Property(value => value.Body).HasMaxLength(BlogDraft.BodyMaxLength).IsRequired();
        blogDraft.Property(value => value.CreatedAtUtc).HasColumnType("datetime2");
        blogDraft.Property(value => value.UpdatedAtUtc).HasColumnType("datetime2");
        blogDraft.Property(value => value.RowVersion).IsRowVersion();
        blogDraft.HasIndex(value => value.ContentId).IsUnique();
        blogDraft.HasOne(value => value.Content)
            .WithOne(value => value.BlogDraft)
            .HasForeignKey<BlogDraft>(value => value.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        var step = modelBuilder.Entity<ContentStep>();
        step.ToTable("ContentSteps");
        step.HasKey(value => value.Id);
        step.Property(value => value.Title).HasMaxLength(200).IsRequired();
        step.Property(value => value.Description).HasMaxLength(4000);
        step.HasIndex(value => new { value.ContentId, value.SortOrder }).IsUnique();
        step.HasIndex(value => value.MediaAssetId);
        step.HasOne(value => value.Content).WithMany(value => value.Steps).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Cascade);
        step.HasOne(value => value.MediaAsset)
            .WithMany(value => value.ContentSteps)
            .HasForeignKey(value => value.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        var tag = modelBuilder.Entity<Tag>();
        tag.ToTable("Tags");
        tag.HasKey(value => value.Id);
        tag.Property(value => value.Name).HasMaxLength(80).IsRequired();
        tag.Property(value => value.NormalizedName).HasMaxLength(80).IsRequired();
        tag.HasIndex(value => value.NormalizedName).IsUnique();

        var contentTag = modelBuilder.Entity<ContentTag>();
        contentTag.ToTable("ContentTags");
        contentTag.HasKey(value => new { value.ContentId, value.TagId });
        contentTag.HasOne(value => value.Content).WithMany(value => value.ContentTags).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Cascade);
        contentTag.HasOne(value => value.Tag).WithMany(value => value.ContentTags).HasForeignKey(value => value.TagId).OnDelete(DeleteBehavior.Cascade);
        contentTag.HasIndex(value => value.TagId);

        var media = modelBuilder.Entity<MediaAsset>();
        media.ToTable("MediaAssets", table =>
            table.HasCheckConstraint("CK_MediaAssets_StorageStatus", "[StorageStatus] IN ('PENDING','READY','FAILED')"));
        media.HasKey(value => value.Id);
        media.Property(value => value.OriginalFileName).HasMaxLength(260).IsRequired();
        media.Property(value => value.StoredFileName).HasMaxLength(100).IsRequired();
        media.Property(value => value.RelativePath).HasMaxLength(500).IsRequired();
        media.Property(value => value.MimeType).HasMaxLength(100).IsRequired();
        media.Property(value => value.Sha256).HasMaxLength(64).IsRequired();
        media.Property(value => value.Description).HasMaxLength(500);
        media.Property(value => value.FailureReason).HasMaxLength(500);
        media.Property(value => value.StorageStatus).HasConversion<string>().HasMaxLength(20);
        media.Property(value => value.CreatedAtUtc).HasColumnType("datetime2");
        media.Property(value => value.DeletedAtUtc).HasColumnType("datetime2");
        media.Property(value => value.RowVersion).IsRowVersion();
        media.HasQueryFilter(value => !value.IsDeleted);
        media.HasIndex(value => value.RelativePath).IsUnique();
        media.HasIndex(value => value.Sha256);
        media.HasIndex(value => new { value.ContentId, value.SortOrder });
        media.HasIndex(value => new { value.ContentId, value.IsSelected, value.SourceTimestampMs });
        media.HasIndex(value => new { value.ContentId, value.Sha256 });
        media.HasOne(value => value.Content).WithMany(value => value.MediaAssets).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Restrict);

        var mediaLink = modelBuilder.Entity<ContentMediaLink>();
        mediaLink.ToTable("ContentMediaLinks");
        mediaLink.HasKey(value => new { value.ContentId, value.MediaAssetId });
        mediaLink.Property(value => value.CreatedAtUtc).HasColumnType("datetime2");
        mediaLink.HasIndex(value => value.MediaAssetId);
        mediaLink.HasOne(value => value.Content)
            .WithMany(value => value.LinkedMedia)
            .HasForeignKey(value => value.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
        mediaLink.HasOne(value => value.MediaAsset)
            .WithMany(value => value.LinkedContents)
            .HasForeignKey(value => value.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        var evidence = modelBuilder.Entity<SourceEvidence>();
        evidence.ToTable("SourceEvidence");
        evidence.HasKey(value => value.Id);
        evidence.Property(value => value.SourceType).HasMaxLength(40).IsRequired();
        evidence.Property(value => value.SourceTitle).HasMaxLength(300);
        evidence.Property(value => value.SourceReference).HasMaxLength(2000);
        evidence.Property(value => value.CapturedAtUtc).HasColumnType("datetime2");
        evidence.HasIndex(value => value.ContentId);
        evidence.HasOne(value => value.Content).WithMany(value => value.SourceEvidence).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDetails(ModelBuilder modelBuilder)
    {
        ConfigureOneToOne(modelBuilder.Entity<PlaceDetails>(), "PlaceDetails", content => content.PlaceDetails);
        ConfigureOneToOne(modelBuilder.Entity<CookingDetails>(), "CookingDetails", content => content.CookingDetails);
        ConfigureOneToOne(modelBuilder.Entity<ExerciseDetails>(), "ExerciseDetails", content => content.ExerciseDetails);
        ConfigureOneToOne(modelBuilder.Entity<CleaningLaundryDetails>(), "CleaningLaundryDetails", content => content.CleaningLaundryDetails);
        ConfigureOneToOne(modelBuilder.Entity<TravelDetails>(), "TravelDetails", content => content.TravelDetails);
        ConfigureOneToOne(modelBuilder.Entity<PhotoDetails>(), "PhotoDetails", content => content.PhotoDetails);
        ConfigureOneToOne(modelBuilder.Entity<StudyDetails>(), "StudyDetails", content => content.StudyDetails);
        ConfigureOneToOne(modelBuilder.Entity<ProductDetails>(), "ProductDetails", content => content.ProductDetails);
        ConfigureOneToOne(modelBuilder.Entity<PhoneComputerDetails>(), "PhoneComputerDetails", content => content.PhoneComputerDetails);
        ConfigureOneToOne(modelBuilder.Entity<TipDetails>(), "TipDetails", content => content.TipDetails);
        ConfigureOneToOne(modelBuilder.Entity<OtherDetails>(), "OtherDetails", content => content.OtherDetails);

        modelBuilder.Entity<PlaceDetails>().Property(value => value.Address).HasMaxLength(500);
        modelBuilder.Entity<PlaceDetails>().Property(value => value.BusinessHours).HasMaxLength(500);
        modelBuilder.Entity<PlaceDetails>().Property(value => value.ParkingInfo).HasMaxLength(500);
        modelBuilder.Entity<PlaceDetails>().Property(value => value.RecommendedMenuOrSpot).HasMaxLength(500);

        var cooking = modelBuilder.Entity<CookingDetails>();
        cooking.Property(value => value.Difficulty).HasMaxLength(50);
        var ingredient = modelBuilder.Entity<CookingIngredient>();
        ingredient.ToTable("CookingIngredients");
        ingredient.HasKey(value => value.Id);
        ingredient.Property(value => value.Name).HasMaxLength(200).IsRequired();
        ingredient.Property(value => value.Quantity).HasMaxLength(100);
        ingredient.Property(value => value.IngredientType).HasMaxLength(30).IsRequired().HasDefaultValue("부재료");
        ingredient.Property(value => value.Note).HasMaxLength(500);
        ingredient.Property(value => value.RowVersion).IsRowVersion();
        ingredient.HasIndex(value => new { value.ContentId, value.SortOrder }).IsUnique();
        ingredient.HasOne(value => value.CookingDetails).WithMany(value => value.Ingredients).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Cascade);

        LimitStrings(modelBuilder);
    }

    private static void ConfigureOneToOne<TDetails>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TDetails> detail,
        string tableName,
        System.Linq.Expressions.Expression<Func<Content, TDetails?>> navigation)
        where TDetails : ContentDetailsBase
    {
        detail.ToTable(tableName);
        detail.HasKey(value => value.ContentId);
        detail.HasOne(value => value.Content).WithOne(navigation).HasForeignKey<TDetails>(value => value.ContentId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void LimitStrings(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(ContentDetailsBase).IsAssignableFrom(type.ClrType)))
        {
            foreach (var property in entityType.GetProperties().Where(property => property.ClrType == typeof(string)))
            {
                property.SetMaxLength(property.Name is "Solution" or "AdditionalInfo" ? 4000 : 500);
            }
        }

        modelBuilder.Entity<ProductDetails>().Property(value => value.Price).HasPrecision(18, 2);
    }

    private void ValidateTrackedDetails()
    {
        foreach (var entry in ChangeTracker.Entries<Content>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            DetailsConsistency.Validate(entry.Entity);
        }
    }
}
