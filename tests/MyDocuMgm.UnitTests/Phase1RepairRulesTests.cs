using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.SearchContents;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class Phase1RepairRulesTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(96)]
    public void ContentSearch_AcceptsWireframePageSizes(int pageSize)
    {
        ContentSearchRules.Validate(Query(pageSize: pageSize));
    }

    [Fact]
    public void ContentSearch_RejectsUnknownAttributeAndUnsafePageSize()
    {
        var unknown = Assert.Throws<DomainRuleException>(() =>
            ContentSearchRules.Validate(Query(category: "COOKING", key: "rawSqlColumn")));
        Assert.Equal("INVALID_ATTRIBUTE_KEY", unknown.Code);

        var size = Assert.Throws<DomainRuleException>(() =>
            ContentSearchRules.Validate(Query(pageSize: 25)));
        Assert.Equal("INVALID_PAGE_SIZE", size.Code);
    }

    [Fact]
    public void SearchAttributeCatalog_ContainsOnlyFixedSafeMappings()
    {
        Assert.True(CategorySearchAttributeCatalog.IsAllowed("COOKING", "primaryIngredient"));
        Assert.True(CategorySearchAttributeCatalog.IsAllowed("PRODUCT", "brand"));
        Assert.False(CategorySearchAttributeCatalog.IsAllowed("COOKING", "brand"));
        Assert.False(CategorySearchAttributeCatalog.IsAllowed("PRODUCT", "DROP TABLE"));
        Assert.Equal(23, CategorySearchAttributeCatalog.All.Count);
    }

    [Fact]
    public async Task MediaSearch_RejectsClientControlledPageSize()
    {
        var service = new MediaService(new EmptyContentRepository(), new EmptyMediaRepository(), new EmptyStorage());
        var error = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SearchAsync(new MediaQuery(Guid.NewGuid(), PageSize: 25), default));
        Assert.Equal("INVALID_MEDIA_PAGE", error.Code);
    }

    private static ContentQuery Query(
        int pageSize = 24,
        string? category = null,
        string? key = null) =>
        new(null, category, key, null, SearchScope.ALL, null, null, null, null, PageSize: pageSize);

    private sealed class EmptyContentRepository : IContentRepository
    {
        public Task AddAsync(Content content, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) => Task.FromResult<Content?>(null);
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult<Tag?>(null);
        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ContentSummary>([], 0, 1, query.PageSize));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EmptyMediaRepository : IMediaAssetRepository
    {
        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken) => Task.FromResult<MediaAsset?>(null);
        public Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MediaAsset>>([]);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaPage([], 0, 0, 0, query.Page, query.PageSize));
    }

    private sealed class EmptyStorage : IMediaStorage
    {
        public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<StoredMedia> StoreAsync(Stream source, string originalFileName, string declaredMimeType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
