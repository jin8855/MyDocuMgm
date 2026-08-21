using MyDocuMgm.Application;
using MyDocuMgm.Application.AnalysisRecommendations;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class AnalysisRecommendationTests
{
    [Fact]
    public async Task Request_PersistsRecommendationsWithoutMutatingContent_AndReplaysIdempotently()
    {
        var content = CreateContent();
        var provider = new Provider(CreateResult(content));
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, provider, TimeProvider.System);

        var first = await service.RequestAsync(content.Id, new("request-1"), default);
        var replay = await service.RequestAsync(content.Id, new("request-1"), default);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal("기존 제목", content.Title);
        Assert.Equal("기존 요약", content.ShortSummary);
        Assert.Equal(AnalysisRecommendationRunStatus.SUCCEEDED, first.Status);
        Assert.Equal(4, first.Items.Count);
        Assert.Contains(provider.LastInput!.Evidence, value => value.EvidenceType == AnalysisRecommendationEvidenceType.DETAIL_CONTENT);
        Assert.Contains(provider.LastInput.Evidence, value => value.EvidenceType == AnalysisRecommendationEvidenceType.MANUAL_CAPTION);
        Assert.DoesNotContain(provider.LastInput.Evidence, value => value.Text.Contains("https://", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Request_UnavailableProviderPersistsSafeFailureWithoutContentMutation()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, new UnavailableProvider(), TimeProvider.System);

        var error = await Assert.ThrowsAsync<AnalysisRecommendationException>(() =>
            service.RequestAsync(content.Id, new("unavailable-1"), default));

        Assert.Equal(AnalysisRecommendationFailureKind.UNAVAILABLE, error.Kind);
        Assert.Equal(AnalysisRecommendationRunStatus.FAILED, repository.Runs.Single().Status);
        Assert.Equal("ANALYSIS_PROVIDER_NOT_CONFIGURED", repository.Runs.Single().ErrorCode);
        Assert.Equal("기존 제목", content.Title);
    }

    [Fact]
    public async Task Request_RejectsProviderEvidenceOwnedByAnotherContent()
    {
        var content = CreateContent();
        var foreignEvidence = new AnalysisRecommendationEvidenceInput(
            AnalysisRecommendationEvidenceType.SOURCE_EVIDENCE,
            Guid.NewGuid(),
            "다른 자료 근거");
        var result = new AnalysisRecommendationProviderResult(false, "fake-v1", [
            new(AnalysisRecommendationKind.TITLE, "추천 제목", "근거", AnalysisRecommendationConfidence.LOW, [foreignEvidence])
        ]);
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, new Provider(result), TimeProvider.System);

        var error = await Assert.ThrowsAsync<AnalysisRecommendationException>(() =>
            service.RequestAsync(content.Id, new("foreign-evidence"), default));

        Assert.Equal(AnalysisRecommendationFailureKind.INVALID_RESPONSE, error.Kind);
        Assert.Empty(repository.Runs.Single().Items);
        Assert.Equal(AnalysisRecommendationRunStatus.FAILED, repository.Runs.Single().Status);
    }

    [Fact]
    public async Task Request_RejectsFabricatedEvidenceAndInfersPartialWhenKindsAreMissing()
    {
        var content = CreateContent();
        var fabricated = new AnalysisRecommendationProviderResult(false, "fake-v1", [
            new(AnalysisRecommendationKind.TITLE, "추천 제목", "근거", AnalysisRecommendationConfidence.HIGH,
                [new(AnalysisRecommendationEvidenceType.DETAIL_CONTENT, null, "현재 본문에 없는 발췌")])
        ]);
        var failedRepository = new Repository(content);
        var failedService = new AnalysisRecommendationService(failedRepository, new Provider(fabricated), TimeProvider.System);

        var error = await Assert.ThrowsAsync<AnalysisRecommendationException>(() =>
            failedService.RequestAsync(content.Id, new("fabricated-evidence"), default));

        Assert.Equal(AnalysisRecommendationFailureKind.INVALID_RESPONSE, error.Kind);

        var validContent = CreateContent();
        var titleOnly = CreateResult(validContent) with { Items = [CreateResult(validContent).Items[0]] };
        var repository = new Repository(validContent);
        var service = new AnalysisRecommendationService(repository, new Provider(titleOnly), TimeProvider.System);

        var result = await service.RequestAsync(validContent.Id, new("inferred-partial"), default);

        Assert.Equal(AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED, result.Status);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Decisions_ApplyModifyAndRejectAtomically_AndReplayWithoutAnotherSave()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, new Provider(CreateResult(content)), TimeProvider.System);
        var run = await service.RequestAsync(content.Id, new("decision-1"), default);
        var title = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.TITLE);
        var summary = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.SUMMARY);
        var category = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.CATEGORY);
        var tag = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.TAG);
        var request = new SaveAnalysisRecommendationDecisionsRequest(
            Convert.ToBase64String(content.RowVersion),
            [
                new(title.Id, AnalysisRecommendationDecision.APPLIED, null),
                new(summary.Id, AnalysisRecommendationDecision.MODIFIED, "사용자 수정 요약"),
                new(category.Id, AnalysisRecommendationDecision.REJECTED, null),
                new(tag.Id, AnalysisRecommendationDecision.APPLIED, null)
            ]);

        var applied = await service.DecideAsync(content.Id, run.Id, request, default);
        var savesAfterApply = repository.SaveCount;
        var replay = await service.DecideAsync(content.Id, run.Id, request, default);

        Assert.Equal("추천 제목", applied.Content.Title);
        Assert.Equal("사용자 수정 요약", applied.Content.ShortSummary);
        Assert.Equal(content.CategoryId, applied.Content.CategoryId);
        Assert.Contains("추천태그", applied.Content.Tags);
        Assert.Equal(savesAfterApply, repository.SaveCount);
        Assert.Equal(applied.Content.Title, replay.Content.Title);
    }

    [Fact]
    public async Task Request_PartialAndCancelledStatesAreRecordedWithoutChangingContent()
    {
        var partialContent = CreateContent();
        var partialResult = CreateResult(partialContent) with
        {
            IsPartial = true,
            Items = [CreateResult(partialContent).Items[0]]
        };
        var partialRepository = new Repository(partialContent);
        var partialService = new AnalysisRecommendationService(partialRepository, new Provider(partialResult), TimeProvider.System);

        var partial = await partialService.RequestAsync(partialContent.Id, new("partial-1"), default);

        Assert.Equal(AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED, partial.Status);
        Assert.Single(partial.Items);
        Assert.Equal("기존 제목", partialContent.Title);

        var cancelledContent = CreateContent();
        var cancelledRepository = new Repository(cancelledContent);
        var cancelledService = new AnalysisRecommendationService(cancelledRepository, new CancellingProvider(), TimeProvider.System);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var error = await Assert.ThrowsAsync<AnalysisRecommendationException>(() =>
            cancelledService.RequestAsync(cancelledContent.Id, new("cancel-1"), source.Token));

        Assert.Equal(AnalysisRecommendationFailureKind.CANCELLED, error.Kind);
        Assert.Equal(AnalysisRecommendationRunStatus.CANCELLED, cancelledRepository.Runs.Single().Status);
        Assert.Equal("기존 제목", cancelledContent.Title);
    }

    [Fact]
    public async Task Request_RejectsInvalidCategoryBeforeARecommendationCanBeApplied()
    {
        var content = CreateContent();
        var invalid = new AnalysisRecommendationProviderResult(false, "fake-v1", [
            new(AnalysisRecommendationKind.CATEGORY, Guid.NewGuid().ToString(), "잘못된 분류", AnalysisRecommendationConfidence.HIGH, [])
        ]);
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, new Provider(invalid), TimeProvider.System);

        var error = await Assert.ThrowsAsync<AnalysisRecommendationException>(() =>
            service.RequestAsync(content.Id, new("invalid-category"), default));

        Assert.Equal(AnalysisRecommendationFailureKind.INVALID_RESPONSE, error.Kind);
        Assert.Equal("기존 제목", content.Title);
    }

    [Fact]
    public async Task Decisions_RejectWrongContentAndRowVersion_AndDoNotDuplicateNormalizedTag()
    {
        var content = CreateContent();
        var duplicateTagResult = new AnalysisRecommendationProviderResult(false, "fake-v1", [
            new(AnalysisRecommendationKind.TAG, " 기존태그 ", "태그 근거", AnalysisRecommendationConfidence.MEDIUM, [])
        ]);
        var repository = new Repository(content);
        var service = new AnalysisRecommendationService(repository, new Provider(duplicateTagResult), TimeProvider.System);
        var run = await service.RequestAsync(content.Id, new("duplicate-tag"), default);
        var item = run.Items.Single();
        var decision = new SaveAnalysisRecommendationDecisionsRequest(
            Convert.ToBase64String(content.RowVersion),
            [new(item.Id, AnalysisRecommendationDecision.APPLIED, null)]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DecideAsync(Guid.NewGuid(), run.Id, decision, default));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.DecideAsync(content.Id, run.Id, decision with { ContentRowVersion = Convert.ToBase64String([9, 9, 9]) }, default));

        var applied = await service.DecideAsync(content.Id, run.Id, decision, default);

        Assert.Single(applied.Content.Tags, value => TagNormalizer.Normalize(value) == TagNormalizer.Normalize("기존태그"));
    }

    private static Content CreateContent()
    {
        var category = CategoryCatalog.All.Single(value => value.Code == "OTHER");
        var content = new Content
        {
            CategoryId = category.Id,
            Category = new Category { Id = category.Id, Code = category.Code, DisplayName = category.DisplayName },
            Title = "기존 제목",
            ShortSummary = "기존 요약",
            DetailContent = "현재 저장된 본문",
            ManualCaption = "사용자가 입력한 Caption",
            PinnedAuthorCommentState = PinnedAuthorCommentState.NONE,
            SourceKind = ContentSourceKind.INSTAGRAM,
            InstagramContentType = InstagramContentType.POST,
            SourceAcquisitionMode = SourceAcquisitionMode.MANUAL,
            IntakeStatus = IntakeStatus.CONTENT_READY,
            CurrentWorkflowStep = WorkflowStep.ANALYSIS_REVIEW,
            RowVersion = [1, 2, 3]
        };
        var tag = new Tag { Name = "기존태그", NormalizedName = TagNormalizer.Normalize("기존태그") };
        content.ContentTags.Add(new ContentTag { Content = content, ContentId = content.Id, Tag = tag, TagId = tag.Id });
        content.SourceEvidence.Add(new SourceEvidence
        {
            Content = content,
            ContentId = content.Id,
            SourceType = "USER_NOTE",
            SourceTitle = "사용자가 저장한 근거"
        });
        return content;
    }

    private static AnalysisRecommendationProviderResult CreateResult(Content content)
    {
        var evidence = new AnalysisRecommendationEvidenceInput(
            AnalysisRecommendationEvidenceType.DETAIL_CONTENT, null, "현재 저장된 본문");
        return new(false, "fake-v1", [
            new(AnalysisRecommendationKind.TITLE, "추천 제목", "제목 근거", AnalysisRecommendationConfidence.HIGH, [evidence]),
            new(AnalysisRecommendationKind.SUMMARY, "추천 요약", "요약 근거", AnalysisRecommendationConfidence.MEDIUM, [evidence]),
            new(AnalysisRecommendationKind.CATEGORY, content.CategoryId.ToString(), "분류 근거", AnalysisRecommendationConfidence.LOW, [evidence]),
            new(AnalysisRecommendationKind.TAG, "추천태그", "태그 근거", AnalysisRecommendationConfidence.MEDIUM, [evidence])
        ]);
    }

    private sealed class Provider(AnalysisRecommendationProviderResult result) : IAnalysisRecommendationProvider
    {
        public string ProviderIdentifier => "fake-provider";
        public int CallCount { get; private set; }
        public AnalysisRecommendationProviderInput? LastInput { get; private set; }

        public Task<AnalysisRecommendationProviderResult> RecommendAsync(
            AnalysisRecommendationProviderInput input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastInput = input;
            return Task.FromResult(result);
        }
    }

    private sealed class UnavailableProvider : IAnalysisRecommendationProvider
    {
        public string ProviderIdentifier => "unavailable";
        public Task<AnalysisRecommendationProviderResult> RecommendAsync(
            AnalysisRecommendationProviderInput input,
            CancellationToken cancellationToken) =>
            throw new AnalysisRecommendationException(
                AnalysisRecommendationFailureKind.UNAVAILABLE,
                "ANALYSIS_PROVIDER_NOT_CONFIGURED",
                "추천 기능을 사용할 수 없습니다. 직접 작성으로 계속할 수 있습니다.");
    }

    private sealed class CancellingProvider : IAnalysisRecommendationProvider
    {
        public string ProviderIdentifier => "cancelling";
        public Task<AnalysisRecommendationProviderResult> RecommendAsync(
            AnalysisRecommendationProviderInput input,
            CancellationToken cancellationToken) => Task.FromCanceled<AnalysisRecommendationProviderResult>(cancellationToken);
    }

    private sealed class Repository(Content content) : IAnalysisRecommendationRepository
    {
        public List<AnalysisRecommendationRun> Runs { get; } = [];
        public int SaveCount { get; private set; }

        public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult(contentId == content.Id ? content : null);
        public Task<AnalysisRecommendationRun?> FindByIdempotencyKeyAsync(Guid contentId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Runs.SingleOrDefault(value => value.ContentId == contentId && value.IdempotencyKey == idempotencyKey));
        public Task<AnalysisRecommendationRun?> FindLatestAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult(Runs.Where(value => value.ContentId == contentId).OrderByDescending(value => value.RequestedAtUtc).FirstOrDefault());
        public Task<IReadOnlyList<AnalysisRecommendationRun>> ListAsync(Guid contentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnalysisRecommendationRun>>(Runs.Where(value => value.ContentId == contentId).Take(limit).ToArray());
        public Task<AnalysisRecommendationRun?> FindRunForDecisionAsync(Guid contentId, Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(Runs.SingleOrDefault(value => value.ContentId == contentId && value.Id == runId));
        public Task AddRunAsync(AnalysisRecommendationRun run, CancellationToken cancellationToken)
        {
            Runs.Add(run);
            return Task.CompletedTask;
        }
        public Task AddItemsAsync(IReadOnlyCollection<AnalysisRecommendationItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) =>
            Task.FromResult(content.ContentTags.Select(value => value.Tag).SingleOrDefault(value => value.NormalizedName == normalizedName));
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
