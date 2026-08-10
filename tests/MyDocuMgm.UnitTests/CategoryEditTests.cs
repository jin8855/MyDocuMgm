using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.EditCategory;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class CategoryEditTests
{
    [Fact]
    public async Task Get_BlocksContentBeforeAnalysisReviewCompletion()
    {
        var content = CreateContent(WorkflowStep.ANALYSIS_REVIEW);
        var service = new CategoryEditService(new Repository(content));

        var error = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetAsync(content.Id, default));

        Assert.Equal("CATEGORY_EDIT_NOT_AVAILABLE", error.Code);
    }

    [Fact]
    public async Task Draft_PreservesWorkflowAndRestoresSavedValues()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new CategoryEditService(repository);
        var cooking = CategoryCatalog.All.Single(category => category.Code == "COOKING");

        var result = await service.ExecuteAsync(content.Id, new(
            cooking.Id,
            new Dictionary<string, string?>
            {
                ["servings"] = "2",
                ["preparationMinutes"] = "15",
                ["difficulty"] = "보통"
            },
            false,
            RowVersion(content)), default);
        var restored = await service.GetAsync(content.Id, default);

        Assert.Equal(WorkflowStep.CATEGORY_EDIT, result.CurrentWorkflowStep);
        Assert.Equal("2", restored.ValuesByCategory["COOKING"]["servings"]);
        Assert.Equal("보통", restored.ValuesByCategory["COOKING"]["difficulty"]);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CategorySwitch_PreservesDormantValuesAndReusesOneToOneRows()
    {
        var content = CreateContent();
        content.PlaceDetails = new PlaceDetails
        {
            ContentId = content.Id,
            Content = content,
            Address = "기존 주소"
        };
        var repository = new Repository(content);
        var service = new CategoryEditService(repository);
        var cooking = CategoryCatalog.All.Single(category => category.Code == "COOKING");

        await service.ExecuteAsync(content.Id, new(
            cooking.Id,
            new Dictionary<string, string?> { ["difficulty"] = "쉬움" },
            false,
            RowVersion(content)), default);
        var firstCooking = content.CookingDetails;
        await service.ExecuteAsync(content.Id, new(
            cooking.Id,
            new Dictionary<string, string?> { ["difficulty"] = "어려움" },
            false,
            RowVersion(content)), default);

        Assert.Equal("기존 주소", content.PlaceDetails.Address);
        Assert.Same(firstCooking, content.CookingDetails);
        Assert.Equal("어려움", content.CookingDetails!.Difficulty);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_SavesValuesAndMovesToMediaInOneSaveBoundary()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new CategoryEditService(repository);
        var photo = CategoryCatalog.All.Single(category => category.Code == "PHOTO");

        var result = await service.ExecuteAsync(content.Id, new(
            photo.Id,
            new Dictionary<string, string?> { ["camera"] = "합성 카메라", ["location"] = "실내" },
            true,
            RowVersion(content)), default);

        Assert.Equal(WorkflowStep.MEDIA, result.CurrentWorkflowStep);
        Assert.Equal("합성 카메라", content.PhotoDetails!.Camera);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task MissingCategoryAndInvalidField_AreRejectedBeforeMutation()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new CategoryEditService(repository);

        var missing = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new(Guid.Empty, new Dictionary<string, string?>(), true, RowVersion(content)),
            default));
        var invalid = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new(content.CategoryId, new Dictionary<string, string?> { ["camera"] = "허용 안 됨" }, true, RowVersion(content)),
            default));

        Assert.Equal("CATEGORY_REQUIRED", missing.Code);
        Assert.Equal("CATEGORY_FIELD_NOT_ALLOWED", invalid.Code);
        Assert.Equal(WorkflowStep.CATEGORY_EDIT, content.CurrentWorkflowStep);
        Assert.Null(content.OtherDetails);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task SaveFailure_DoesNotChangeDurableSnapshot()
    {
        var durable = CreateContent();
        var repository = new Repository(durable, failSave: true, returnClone: true);
        var service = new CategoryEditService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            durable.Id,
            new(durable.CategoryId, new Dictionary<string, string?> { ["customLabel"] = "실패 값" }, true, RowVersion(durable)),
            default));

        Assert.Equal(WorkflowStep.CATEGORY_EDIT, durable.CurrentWorkflowStep);
        Assert.Null(durable.OtherDetails);
    }

    private static Content CreateContent(WorkflowStep step = WorkflowStep.CATEGORY_EDIT) => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "분류 편집 대상",
        ShortSummary = "사용자가 확정한 요약",
        CurrentWorkflowStep = step,
        RowVersion = [1, 2, 3]
    };

    private static string RowVersion(Content content) => Convert.ToBase64String(content.RowVersion);

    private sealed class Repository(Content durable, bool failSave = false, bool returnClone = false) : IContentRepository
    {
        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(id != durable.Id ? null : returnClone ? Clone(durable) : durable);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (failSave) throw new InvalidOperationException("결정적 저장 실패");
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ContentSummary>([], 0, query.Page, query.PageSize));
        public Task AddAsync(Content content, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult<Tag?>(null);
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;

        private static Content Clone(Content source) => new()
        {
            Id = source.Id,
            CategoryId = source.CategoryId,
            Title = source.Title,
            ShortSummary = source.ShortSummary,
            CurrentWorkflowStep = source.CurrentWorkflowStep,
            RowVersion = [.. source.RowVersion]
        };
    }
}
