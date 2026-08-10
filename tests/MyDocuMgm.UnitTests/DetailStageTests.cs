using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.ReviewDetail;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class DetailStageTests
{
    [Fact]
    public async Task Get_BlocksDirectEntryBeforeImageCompletes()
    {
        var content = CreateContent(WorkflowStep.MEDIA);
        var service = new DetailStageService(new Repository(content));

        var error = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetAsync(content.Id, default));

        Assert.Equal("DETAIL_STAGE_NOT_AVAILABLE", error.Code);
    }

    [Fact]
    public async Task Get_IntegratesOnlyTheCurrentContentsPriorStageData()
    {
        var content = CreateCookingContent();
        var foreignOwnerId = Guid.NewGuid();
        var media = CreateMedia(foreignOwnerId);
        content.LinkedMedia.Add(new ContentMediaLink
        {
            ContentId = content.Id,
            MediaAssetId = media.Id,
            MediaAsset = media
        });
        var service = new DetailStageService(new Repository(content));

        var result = await service.GetAsync(content.Id, default);

        Assert.Equal("합성 제목", result.Title);
        Assert.Equal("합성 요약", result.ShortSummary);
        Assert.Equal("https://www.instagram.com/p/synthetic/", result.OriginalUrl);
        Assert.Equal("합성 caption", result.ManualCaption);
        Assert.Equal("COOKING", result.CategoryCode);
        Assert.Equal("2", result.CategoryValues["servings"]);
        Assert.Equal("새우", Assert.Single(result.Ingredients).Name);
        Assert.Equal(foreignOwnerId, Assert.Single(result.LinkedMedia).OwnerContentId);
        Assert.Empty(result.EditableFields);
    }

    [Fact]
    public async Task Get_AnotherContentIdDoesNotLeakData()
    {
        var content = CreateContent();
        var service = new DetailStageService(new Repository(content));

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Draft_WithNoOwnedFieldsStaysInDetailAndPreservesPriorData()
    {
        var content = CreateCookingContent();
        var repository = new Repository(content);
        var service = new DetailStageService(repository);

        var first = await service.ExecuteAsync(content.Id, new(new Dictionary<string, string?>(), false, RowVersion(content)), default);
        var second = await service.ExecuteAsync(content.Id, new(null, false, RowVersion(content)), default);

        Assert.Equal(WorkflowStep.DETAIL, first.CurrentWorkflowStep);
        Assert.Equal(WorkflowStep.DETAIL, second.CurrentWorkflowStep);
        Assert.Equal("합성 제목", content.Title);
        Assert.Equal("합성 요약", content.ShortSummary);
        Assert.Equal("보통", content.CookingDetails!.Difficulty);
        Assert.Single(content.CookingDetails.Ingredients);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Draft_RejectsUnapprovedFieldsWithoutMutation()
    {
        var content = CreateCookingContent();
        var repository = new Repository(content);
        var service = new DetailStageService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new(new Dictionary<string, string?> { ["title"] = "덮어쓰기" }, false, RowVersion(content)),
            default));

        Assert.Equal("DETAIL_FIELD_NOT_EDITABLE", error.Code);
        Assert.Equal("합성 제목", content.Title);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_BlocksWhenRequiredPriorDataIsMissing()
    {
        var content = CreateContent();
        content.Title = "   ";
        var repository = new Repository(content);
        var service = new DetailStageService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new(null, true, RowVersion(content)),
            default));

        Assert.Equal("DETAIL_REQUIRED_DATA_MISSING", error.Code);
        Assert.Equal(WorkflowStep.DETAIL, content.CurrentWorkflowStep);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_AdvancesToBlogDraftInOneSave()
    {
        var content = CreateCookingContent();
        var repository = new Repository(content);
        var service = new DetailStageService(repository);

        var result = await service.ExecuteAsync(content.Id, new(null, true, RowVersion(content)), default);

        Assert.Equal(WorkflowStep.BLOG_DRAFT, result.CurrentWorkflowStep);
        Assert.Equal("보통", result.CategoryValues["difficulty"]);
        Assert.Single(result.Ingredients);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task SaveFailure_PreservesDurableDataMediaAndWorkflow()
    {
        var durable = CreateCookingContent();
        var media = CreateMedia(durable.Id);
        durable.LinkedMedia.Add(new ContentMediaLink { ContentId = durable.Id, MediaAssetId = media.Id, MediaAsset = media });
        var repository = new Repository(durable, failSave: true, returnClone: true);
        var service = new DetailStageService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            durable.Id,
            new(null, true, RowVersion(durable)),
            default));

        Assert.Equal(WorkflowStep.DETAIL, durable.CurrentWorkflowStep);
        Assert.Equal("합성 제목", durable.Title);
        Assert.Equal("보통", durable.CookingDetails!.Difficulty);
        Assert.Equal(media.Id, Assert.Single(durable.LinkedMedia).MediaAssetId);
        Assert.False(media.IsDeleted);
    }

    [Fact]
    public async Task SaveAfterCompletion_IsRejectedWithoutDuplicateWrite()
    {
        var content = CreateContent(WorkflowStep.BLOG_DRAFT);
        var repository = new Repository(content);
        var service = new DetailStageService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new(null, true, RowVersion(content)),
            default));

        Assert.Equal("DETAIL_STAGE_ALREADY_COMPLETED", error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Content CreateContent(WorkflowStep step = WorkflowStep.DETAIL) => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "합성 제목",
        ShortSummary = "합성 요약",
        DetailContent = "합성 수동 본문",
        OriginalUrl = "https://www.instagram.com/p/synthetic/",
        NormalizedUrl = "https://www.instagram.com/p/synthetic/",
        SourceKind = ContentSourceKind.INSTAGRAM,
        InstagramContentType = global::MyDocuMgm.Domain.InstagramContentType.POST,
        ManualCaption = "합성 caption",
        PinnedAuthorCommentState = global::MyDocuMgm.Domain.PinnedAuthorCommentState.NONE,
        CurrentWorkflowStep = step,
        RowVersion = [1, 2, 3],
        OtherDetails = new OtherDetails { CustomLabel = "합성 분류 값" }
    };

    private static Content CreateCookingContent()
    {
        var content = CreateContent();
        content.CategoryId = CategoryCatalog.All.Single(category => category.Code == "COOKING").Id;
        content.OtherDetails = null;
        content.CookingDetails = new CookingDetails
        {
            ContentId = content.Id,
            Content = content,
            Servings = 2,
            Difficulty = "보통"
        };
        content.CookingDetails.Ingredients.Add(new CookingIngredient
        {
            ContentId = content.Id,
            SortOrder = 1,
            Name = "새우",
            IngredientType = "주재료",
            IsPrimary = true
        });
        return content;
    }

    private static MediaAsset CreateMedia(Guid ownerContentId) => new()
    {
        ContentId = ownerContentId,
        OriginalFileName = "synthetic.png",
        StoredFileName = "synthetic.png",
        RelativePath = $"media/{ownerContentId:N}/{Guid.NewGuid():N}/original/synthetic.png",
        MimeType = "image/png",
        SizeBytes = 68,
        Sha256 = new string('A', 64),
        Width = 1,
        Height = 1,
        StorageStatus = MediaStorageStatus.READY
    };

    private static string RowVersion(Content content) => Convert.ToBase64String(content.RowVersion);

    private sealed class Repository : IDetailStageRepository
    {
        private readonly Content _working;
        private readonly bool _failSave;

        public Repository(Content durable, bool failSave = false, bool returnClone = false)
        {
            _working = returnClone ? Clone(durable) : durable;
            _failSave = failSave;
        }

        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(contentId == _working.Id ? _working : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_failSave) throw new InvalidOperationException("synthetic detail save failure");
            SaveCount++;
            return Task.CompletedTask;
        }

        private static Content Clone(Content source)
        {
            var clone = new Content
            {
                Id = source.Id,
                CategoryId = source.CategoryId,
                Title = source.Title,
                ShortSummary = source.ShortSummary,
                DetailContent = source.DetailContent,
                OriginalUrl = source.OriginalUrl,
                CurrentWorkflowStep = source.CurrentWorkflowStep,
                RowVersion = [.. source.RowVersion]
            };
            if (source.CookingDetails is { } cooking)
            {
                clone.CookingDetails = new CookingDetails
                {
                    ContentId = clone.Id,
                    Content = clone,
                    Servings = cooking.Servings,
                    Difficulty = cooking.Difficulty
                };
                foreach (var ingredient in cooking.Ingredients)
                {
                    clone.CookingDetails.Ingredients.Add(new CookingIngredient
                    {
                        Id = ingredient.Id,
                        ContentId = clone.Id,
                        SortOrder = ingredient.SortOrder,
                        Name = ingredient.Name,
                        IngredientType = ingredient.IngredientType,
                        IsPrimary = ingredient.IsPrimary
                    });
                }
            }
            foreach (var link in source.LinkedMedia)
            {
                clone.LinkedMedia.Add(new ContentMediaLink
                {
                    ContentId = clone.Id,
                    MediaAssetId = link.MediaAssetId,
                    MediaAsset = link.MediaAsset
                });
            }
            return clone;
        }
    }
}
