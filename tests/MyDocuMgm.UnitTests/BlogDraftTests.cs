using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.WriteBlogDraft;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class BlogDraftTests
{
    [Fact]
    public async Task Get_BlocksBeforeDetailCompletionAndDoesNotCreateDraft()
    {
        var content = CreateContent(WorkflowStep.DETAIL);
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.GetAsync(content.Id, default));

        Assert.Equal("BLOG_DRAFT_NOT_AVAILABLE", error.Code);
        Assert.Null(content.BlogDraft);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Get_UsesApprovedFallbackWithoutPersisting()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var result = await service.GetAsync(content.Id, default);

        Assert.Equal("분석 제목", result.Title);
        Assert.Equal("직접 입력 본문\n둘째 줄", result.Body);
        Assert.False(result.HasSavedDraft);
        Assert.Null(result.DraftRowVersion);
        Assert.Null(content.BlogDraft);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Get_ExistingDraftWinsOverChangedSourceValues()
    {
        var content = CreateContent();
        content.BlogDraft = Draft(content, "저장 제목", "저장 본문");
        content.Title = "변경된 분석 제목";
        content.DetailContent = "변경된 원문";
        var service = new BlogDraftService(new Repository(content));

        var result = await service.GetAsync(content.Id, default);

        Assert.True(result.HasSavedDraft);
        Assert.Equal("저장 제목", result.Title);
        Assert.Equal("저장 본문", result.Body);
    }

    [Fact]
    public async Task PartialSaves_PreserveOmittedFieldAndCreateOnlyOneDraft()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var first = await service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = "초안 제목",
            Complete = false,
            ContentRowVersion = RowVersion(content)
        }, default);
        var second = await service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Body = "교체 본문\n줄바꿈",
            Complete = false,
            ContentRowVersion = RowVersion(content),
            DraftRowVersion = first.DraftRowVersion
        }, default);

        Assert.Equal("초안 제목", second.Title);
        Assert.Equal("교체 본문\n줄바꿈", second.Body);
        Assert.Equal(WorkflowStep.BLOG_DRAFT, content.CurrentWorkflowStep);
        Assert.Equal("분석 제목", content.Title);
        Assert.Equal("분석 요약", content.ShortSummary);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task Save_RejectsSchemaLengthBoundariesBeforeWrite()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var titleError = await Assert.ThrowsAsync<DomainRuleException>(() => service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = new string('가', BlogDraft.TitleMaxLength + 1),
            ContentRowVersion = RowVersion(content)
        }, default));
        var bodyError = await Assert.ThrowsAsync<DomainRuleException>(() => service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Body = new string('나', BlogDraft.BodyMaxLength + 1),
            ContentRowVersion = RowVersion(content)
        }, default));

        Assert.Equal("BLOG_DRAFT_TITLE_TOO_LONG", titleError.Code);
        Assert.Equal("BLOG_DRAFT_BODY_TOO_LONG", bodyError.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData("   ", "본문", "BLOG_DRAFT_TITLE_REQUIRED")]
    [InlineData("제목", "\r\n\t", "BLOG_DRAFT_BODY_REQUIRED")]
    public async Task Complete_RejectsWhitespaceRequiredValues(string title, string body, string code)
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = title,
            Body = body,
            Complete = true,
            ContentRowVersion = RowVersion(content)
        }, default));

        Assert.Equal(code, error.Code);
        Assert.Equal(WorkflowStep.BLOG_DRAFT, content.CurrentWorkflowStep);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_SavesDraftAndMovesToCompletedInOneSave()
    {
        var content = CreateContent();
        var media = CreateMedia(content.Id);
        content.LinkedMedia.Add(new ContentMediaLink { ContentId = content.Id, MediaAssetId = media.Id, MediaAsset = media });
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var result = await service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = "완료 제목",
            Body = "완료 본문",
            Complete = true,
            ContentRowVersion = RowVersion(content)
        }, default);

        Assert.Equal(WorkflowStep.COMPLETED, result.CurrentWorkflowStep);
        Assert.Equal("완료 제목", content.BlogDraft!.Title);
        Assert.Equal("분석 제목", content.Title);
        Assert.Equal(media.Id, Assert.Single(content.LinkedMedia).MediaAssetId);
        Assert.False(media.IsDeleted);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task SaveFailure_PreservesDurableDraftWorkflowAndPriorData()
    {
        var durable = CreateContent();
        durable.BlogDraft = Draft(durable, "기존 제목", "기존 본문");
        var repository = new Repository(durable, failSave: true, returnClone: true);
        var service = new BlogDraftService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(durable.Id, new SaveBlogDraftRequest
        {
            Title = "실패 제목",
            Body = "실패 본문",
            Complete = true,
            ContentRowVersion = RowVersion(durable),
            DraftRowVersion = RowVersion(durable.BlogDraft)
        }, default));

        Assert.Equal(WorkflowStep.BLOG_DRAFT, durable.CurrentWorkflowStep);
        Assert.Equal("기존 제목", durable.BlogDraft.Title);
        Assert.Equal("기존 본문", durable.BlogDraft.Body);
        Assert.Equal("분석 제목", durable.Title);
        Assert.Equal("직접 입력 본문\n둘째 줄", durable.DetailContent);
    }

    [Fact]
    public async Task LostSuccessRetry_IsIdempotentAndChangedRetryIsRejected()
    {
        var content = CreateContent(WorkflowStep.COMPLETED);
        content.BlogDraft = Draft(content, "완료 제목", "완료 본문");
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var retry = await service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = "완료 제목",
            Body = "완료 본문",
            Complete = true,
            ContentRowVersion = "stale-response-version",
            DraftRowVersion = "stale-response-version"
        }, default);
        var omitted = await Assert.ThrowsAsync<DomainRuleException>(() => service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Complete = true
        }, default));
        var changed = await Assert.ThrowsAsync<DomainRuleException>(() => service.SaveAsync(content.Id, new SaveBlogDraftRequest
        {
            Title = "다른 제목",
            Body = "완료 본문",
            Complete = true
        }, default));

        Assert.Equal(WorkflowStep.COMPLETED, retry.CurrentWorkflowStep);
        Assert.Equal("BLOG_DRAFT_ALREADY_COMPLETED", omitted.Code);
        Assert.Equal("BLOG_DRAFT_ALREADY_COMPLETED", changed.Code);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task GetCompletion_RequiresCompletedSavedDraftAndNeverWrites()
    {
        var content = CreateContent(WorkflowStep.COMPLETED);
        content.BlogDraft = Draft(content, "저장된 완료 제목", "첫 줄\n둘째 줄");
        var repository = new Repository(content);
        var service = new BlogDraftService(repository);

        var result = await service.GetCompletionAsync(content.Id, default);

        Assert.Equal(WorkflowStep.COMPLETED, result.CurrentWorkflowStep);
        Assert.True(result.HasSavedDraft);
        Assert.Equal("저장된 완료 제목", result.Title);
        Assert.Equal("첫 줄\n둘째 줄", result.Body);
        Assert.Equal("분석 제목", result.AnalysisTitle);
        Assert.Equal("분석 요약", result.ShortSummary);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task GetCompletion_RejectsIncompleteMissingDraftAndForeignContentWithoutWrite()
    {
        var incomplete = CreateContent(WorkflowStep.BLOG_DRAFT);
        incomplete.BlogDraft = Draft(incomplete, "초안", "본문");
        var incompleteRepository = new Repository(incomplete);
        var incompleteService = new BlogDraftService(incompleteRepository);
        var incompleteError = await Assert.ThrowsAsync<DomainRuleException>(
            () => incompleteService.GetCompletionAsync(incomplete.Id, default));

        var missing = CreateContent(WorkflowStep.COMPLETED);
        var missingRepository = new Repository(missing);
        var missingService = new BlogDraftService(missingRepository);
        var missingError = await Assert.ThrowsAsync<DomainRuleException>(
            () => missingService.GetCompletionAsync(missing.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(
            () => missingService.GetCompletionAsync(Guid.NewGuid(), default));

        Assert.Equal("COMPLETION_NOT_AVAILABLE", incompleteError.Code);
        Assert.Equal("COMPLETION_BLOG_DRAFT_MISSING", missingError.Code);
        Assert.Equal(0, incompleteRepository.SaveCount);
        Assert.Equal(0, missingRepository.SaveCount);
    }

    [Fact]
    public async Task Get_ForeignContentIdDoesNotLeakDraft()
    {
        var content = CreateContent();
        content.BlogDraft = Draft(content, "비공개 제목", "비공개 본문");
        var service = new BlogDraftService(new Repository(content));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), default));
    }

    private static Content CreateContent(WorkflowStep step = WorkflowStep.BLOG_DRAFT) => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "분석 제목",
        ShortSummary = "분석 요약",
        DetailContent = "직접 입력 본문\n둘째 줄",
        ManualCaption = "Caption fallback",
        CurrentWorkflowStep = step,
        RowVersion = [1, 2, 3],
        OtherDetails = new OtherDetails { CustomLabel = "합성 분류" }
    };

    private static BlogDraft Draft(Content content, string title, string body) => new()
    {
        ContentId = content.Id,
        Content = content,
        Title = title,
        Body = body,
        RowVersion = [4, 5, 6]
    };

    private static MediaAsset CreateMedia(Guid contentId) => new()
    {
        ContentId = contentId,
        OriginalFileName = "synthetic.png",
        StoredFileName = "synthetic.png",
        RelativePath = $"media/{contentId:N}/{Guid.NewGuid():N}/original/synthetic.png",
        MimeType = "image/png",
        SizeBytes = 68,
        Sha256 = new string('A', 64),
        Width = 1,
        Height = 1,
        StorageStatus = MediaStorageStatus.READY
    };

    private static string RowVersion(Content content) => Convert.ToBase64String(content.RowVersion);
    private static string RowVersion(BlogDraft draft) => Convert.ToBase64String(draft.RowVersion);

    private sealed class Repository : IBlogDraftRepository
    {
        private readonly Content _working;
        private readonly bool _failSave;

        public Repository(Content durable, bool failSave = false, bool returnClone = false)
        {
            _working = returnClone ? Clone(durable) : durable;
            _failSave = failSave;
        }

        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(contentId == _working.Id ? _working : null);

        public void Add(BlogDraft draft) => AddCount++;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_failSave) throw new InvalidOperationException("synthetic blog draft save failure");
            SaveCount++;
            if (_working.BlogDraft is { } draft) draft.RowVersion = [(byte)(10 + SaveCount)];
            return Task.CompletedTask;
        }

        private static Content Clone(Content source)
        {
            var clone = CreateContent(source.CurrentWorkflowStep);
            clone.Id = source.Id;
            clone.CategoryId = source.CategoryId;
            clone.Title = source.Title;
            clone.ShortSummary = source.ShortSummary;
            clone.DetailContent = source.DetailContent;
            clone.RowVersion = [.. source.RowVersion];
            if (source.BlogDraft is { } draft)
            {
                clone.BlogDraft = new BlogDraft
                {
                    Id = draft.Id,
                    ContentId = clone.Id,
                    Content = clone,
                    Title = draft.Title,
                    Body = draft.Body,
                    RowVersion = [.. draft.RowVersion]
                };
            }
            return clone;
        }
    }
}
