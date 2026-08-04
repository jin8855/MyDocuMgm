using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.Data;

namespace MyDocuMgm.IntegrationTests;

public sealed class EfModelTests
{
    private static MyDocuMgmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
            .UseSqlServer("Server=(local);Database=MyDocuMgm;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        return new MyDocuMgmDbContext(options);
    }

    [Fact]
    public void ModelContainsExpectedTablesWithoutOpeningConnection()
    {
        using var context = CreateContext();
        var tables = context.Model.GetEntityTypes().Select(type => type.GetTableName()).ToHashSet();

        Assert.Contains("Contents", tables);
        Assert.Contains("Categories", tables);
        Assert.Contains("MediaAssets", tables);
        Assert.Contains("CookingIngredients", tables);
        Assert.Contains("CategorySearchAttributes", tables);
        Assert.Contains("OtherDetails", tables);
        Assert.Contains("ContentMediaLinks", tables);
        Assert.DoesNotContain("Publication", tables);
        Assert.DoesNotContain("ImportJobs", tables);
    }

    [Fact]
    public void ModelHasRowVersionsUniqueIndexesAndSoftDeleteFilters()
    {
        using var context = CreateContext();
        var content = context.Model.FindEntityType(typeof(Content))!;
        var media = context.Model.FindEntityType(typeof(MediaAsset))!;
        var category = context.Model.FindEntityType(typeof(Category))!;
        var ingredient = context.Model.FindEntityType(typeof(CookingIngredient))!;
        var step = context.Model.FindEntityType(typeof(ContentStep))!;
        var tag = context.Model.FindEntityType(typeof(Tag))!;
        var mediaLink = context.Model.FindEntityType(typeof(ContentMediaLink))!;

        Assert.True(content.FindProperty(nameof(Content.RowVersion))!.IsConcurrencyToken);
        Assert.True(media.FindProperty(nameof(MediaAsset.RowVersion))!.IsConcurrencyToken);
        Assert.True(category.FindProperty(nameof(Category.RowVersion))!.IsConcurrencyToken);
        Assert.True(ingredient.FindProperty(nameof(CookingIngredient.RowVersion))!.IsConcurrencyToken);
        Assert.NotEmpty(content.GetDeclaredQueryFilters());
        Assert.NotEmpty(media.GetDeclaredQueryFilters());
        Assert.Contains(category.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Category.Code));
        Assert.Contains(tag.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Tag.NormalizedName));
        Assert.Contains(media.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(MediaAsset.RelativePath));
        Assert.Contains(media.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(MediaAsset.ContentId), nameof(MediaAsset.IsSelected), nameof(MediaAsset.SourceTimestampMs)]));
        Assert.Contains(step.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(MediaAsset) &&
            key.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(content.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(Content.NormalizedUrlHash) &&
            index.GetFilter() == "[NormalizedUrlHash] IS NOT NULL");
        Assert.Equal(
            [nameof(ContentMediaLink.ContentId), nameof(ContentMediaLink.MediaAssetId)],
            mediaLink.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(mediaLink.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(MediaAsset) &&
            key.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void CategorySeedIdsAreStable()
    {
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var seed = designTimeModel.FindEntityType(typeof(Category))!.GetSeedData();

        Assert.Equal(11, seed.Count());
        Assert.Equal(
            CategoryCatalog.All.Select(value => value.Id).Order(),
            seed.Select(value => Assert.IsType<Guid>(value[nameof(Category.Id)])).Order());
    }

    [Fact]
    public void SearchAttributeSeedsAreSafeAndStable()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var seed = model.FindEntityType(typeof(CategorySearchAttribute))!.GetSeedData().ToArray();

        Assert.Equal(CategorySearchAttributeCatalog.All.Count, seed.Length);
        Assert.Equal(
            CategorySearchAttributeCatalog.All.Select(value => value.Id).Order(),
            seed.Select(value => Assert.IsType<Guid>(value[nameof(CategorySearchAttribute.Id)])).Order());
    }
}
