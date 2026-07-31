using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.SearchContents;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class Phase1IntegrationRepairTests
{
    private static readonly Guid CookingCategoryId =
        CategoryCatalog.All.Single(category => category.Code == "COOKING").Id;

    [Theory]
    [InlineData(WorkflowStep.URL)]
    [InlineData(WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData(WorkflowStep.CATEGORY_EDIT)]
    [InlineData(WorkflowStep.MEDIA)]
    [InlineData(WorkflowStep.DETAIL)]
    [InlineData(WorkflowStep.BLOG_DRAFT)]
    [InlineData(WorkflowStep.COMPLETED)]
    public void ContentSearch_AcceptsEveryDefinedWorkflowStep(WorkflowStep step)
    {
        ContentSearchRules.Validate(Query(workflowStep: step));
    }

    [Fact]
    public void ContentSearch_RejectsUndefinedWorkflowStep()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            ContentSearchRules.Validate(Query(workflowStep: (WorkflowStep)7)));

        Assert.Equal("INVALID_WORKFLOW_STEP", error.Code);
    }

    [Fact]
    public async Task ContentUpdate_PreservesExistingLink_AddsNewTag_Deduplicates_AndKeepsSharedTag()
    {
        var content = ContentWithTags("기존", "공유");
        var existingLink = content.ContentTags.Single(link => link.Tag.Name == "기존");
        var sharedLink = content.ContentTags.Single(link => link.Tag.Name == "공유");
        var otherContentLink = new ContentTag
        {
            ContentId = Guid.NewGuid(),
            TagId = sharedLink.TagId,
            Tag = sharedLink.Tag
        };
        sharedLink.Tag.ContentTags.Add(otherContentLink);

        var repository = new TrackingSensitiveRepository(content, existingLink);
        var service = new ContentService(repository);

        var updated = await service.UpdateAsync(
            content.Id,
            Request(content, ["기존", "신규", " 신규 "]),
            default);

        Assert.Equal(["기존", "신규"], updated.Tags);
        Assert.Contains(content.ContentTags, link => ReferenceEquals(link, existingLink));
        Assert.DoesNotContain(content.ContentTags, link => ReferenceEquals(link, sharedLink));
        Assert.Single(content.ContentTags, link => link.Tag.NormalizedName == "신규");
        Assert.Contains(sharedLink.Tag, repository.Tags);
        Assert.Contains(otherContentLink, sharedLink.Tag.ContentTags);
        Assert.Single(repository.AddedTags);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ContentUpdate_RejectsOverlongTagAsContractError()
    {
        var content = ContentWithTags("기존");
        var repository = new TrackingSensitiveRepository(content, content.ContentTags.Single());
        var service = new ContentService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.UpdateAsync(content.Id, Request(content, [new string('가', 81)]), default));

        Assert.Equal("INVALID_TAG", error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ContentUpdate_StaleRowVersionDoesNotTouchTags()
    {
        var content = ContentWithTags("기존");
        var originalLink = content.ContentTags.Single();
        var repository = new TrackingSensitiveRepository(content, originalLink);
        var service = new ContentService(repository);
        var request = Request(content, ["기존", "신규"]) with { RowVersion = Convert.ToBase64String([9, 9, 9]) };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.UpdateAsync(content.Id, request, default));

        Assert.Single(content.ContentTags);
        Assert.Same(originalLink, content.ContentTags.Single());
        Assert.Equal(0, repository.FindTagCount);
        Assert.Equal(0, repository.SaveCount);
    }

    private static ContentQuery Query(WorkflowStep? workflowStep = null) =>
        new(
            Keyword: null,
            MajorCategory: null,
            AttributeKey: null,
            AttributeValue: null,
            SearchScope: SearchScope.ALL,
            CategoryId: null,
            Status: null,
            WorkflowStep: workflowStep,
            IsFavorite: null,
            PageSize: 24);

    private static Content ContentWithTags(params string[] names)
    {
        var content = new Content
        {
            CategoryId = CookingCategoryId,
            Title = "태그 수정 검증",
            RowVersion = [1, 2, 3]
        };

        foreach (var name in names)
        {
            var tag = new Tag { Name = name, NormalizedName = TagNormalizer.Normalize(name) };
            var link = new ContentTag
            {
                Content = content,
                ContentId = content.Id,
                Tag = tag,
                TagId = tag.Id
            };
            content.ContentTags.Add(link);
            tag.ContentTags.Add(link);
        }

        return content;
    }

    private static SaveContentRequest Request(Content content, IReadOnlyList<string> tags) =>
        new(
            content.CategoryId,
            content.Title,
            content.ShortSummary,
            content.DetailContent,
            content.Status,
            content.Visibility,
            content.IsFavorite,
            content.ExperienceStatus,
            tags,
            Convert.ToBase64String(content.RowVersion));

    private sealed class TrackingSensitiveRepository(Content content, ContentTag originalLink) : IContentRepository
    {
        private readonly List<Tag> tags =
            content.ContentTags.Select(link => link.Tag).ToList();

        public IReadOnlyCollection<Tag> Tags => tags;
        public List<Tag> AddedTags { get; } = [];

        public int FindTagCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult(id == content.Id ? content : null);

        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken)
        {
            FindTagCount++;
            return Task.FromResult(Tags.SingleOrDefault(tag => tag.NormalizedName == normalizedName));
        }

        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken)
        {
            tags.Add(tag);
            AddedTags.Add(tag);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!content.ContentTags.Contains(originalLink))
            {
                throw new InvalidOperationException("기존 ContentTag 연결이 새 인스턴스로 교체되었습니다.");
            }

            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(Content added, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ContentSummary>([], 0, query.Page, query.PageSize));
    }
}
