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
        Assert.Contains("OtherDetails", tables);
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
        var tag = context.Model.FindEntityType(typeof(Tag))!;

        Assert.True(content.FindProperty(nameof(Content.RowVersion))!.IsConcurrencyToken);
        Assert.True(media.FindProperty(nameof(MediaAsset.RowVersion))!.IsConcurrencyToken);
        Assert.NotEmpty(content.GetDeclaredQueryFilters());
        Assert.NotEmpty(media.GetDeclaredQueryFilters());
        Assert.Contains(category.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Category.Code));
        Assert.Contains(tag.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Tag.NormalizedName));
        Assert.Contains(media.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(MediaAsset.RelativePath));
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
}
