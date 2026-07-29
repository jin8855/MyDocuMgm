using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class MyDocuMgmDbContext(DbContextOptions<MyDocuMgmDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<ContentStep> ContentSteps => Set<ContentStep>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
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
        category.HasIndex(value => value.Code).IsUnique();
        category.HasIndex(value => value.SortOrder).IsUnique();
        category.HasData(CategoryCatalog.All.Select(value => new Category
        {
            Id = value.Id,
            SortOrder = value.SortOrder,
            Code = value.Code,
            DisplayName = value.DisplayName
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
        });
        content.HasKey(value => value.Id);
        content.Property(value => value.Title).HasMaxLength(200).IsRequired();
        content.Property(value => value.ShortSummary).HasMaxLength(500);
        content.Property(value => value.DetailContent).HasMaxLength(20_000);
        content.Property(value => value.Status).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.Visibility).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.ExperienceStatus).HasConversion<string>().HasMaxLength(30);
        content.Property(value => value.CreatedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.UpdatedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.DeletedAtUtc).HasColumnType("datetime2");
        content.Property(value => value.RowVersion).IsRowVersion();
        content.HasQueryFilter(value => !value.IsDeleted);
        content.HasOne(value => value.Category).WithMany(value => value.Contents).HasForeignKey(value => value.CategoryId).OnDelete(DeleteBehavior.Restrict);
        content.HasIndex(value => new { value.IsDeleted, value.UpdatedAtUtc });
        content.HasIndex(value => new { value.CategoryId, value.Status, value.IsFavorite });
        content.HasIndex(value => value.Title);
    }

    private static void ConfigureChildren(ModelBuilder modelBuilder)
    {
        var step = modelBuilder.Entity<ContentStep>();
        step.ToTable("ContentSteps");
        step.HasKey(value => value.Id);
        step.Property(value => value.Title).HasMaxLength(200).IsRequired();
        step.Property(value => value.Description).HasMaxLength(4000);
        step.HasIndex(value => new { value.ContentId, value.SortOrder }).IsUnique();
        step.HasOne(value => value.Content).WithMany(value => value.Steps).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Cascade);

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
        media.HasOne(value => value.Content).WithMany(value => value.MediaAssets).HasForeignKey(value => value.ContentId).OnDelete(DeleteBehavior.Restrict);

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
        ingredient.Property(value => value.Note).HasMaxLength(500);
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
