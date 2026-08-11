using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.ReviewAnalysis;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class AnalysisReviewTests
{
    [Theory]
    [InlineData(false, WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData(true, WorkflowStep.CATEGORY_EDIT)]
    public async Task Save_UsesExistingFieldsAndMovesThroughApprovedManualReview(
        bool complete,
        WorkflowStep expectedStep)
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new AnalysisReviewService(repository);

        var result = await service.ExecuteAsync(
            content.Id,
            new("  사용자가 작성한 제목  ", "  사용자가 작성한 요약  ", complete, RowVersion(content)),
            default);

        Assert.Equal("사용자가 작성한 제목", result.Title);
        Assert.Equal("사용자가 작성한 요약", result.ShortSummary);
        Assert.Equal(expectedStep, result.CurrentWorkflowStep);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Save_RejectsOverlongSummaryBeforeMutation()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        var service = new AnalysisReviewService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new("제목", new string('가', 501), false, RowVersion(content)),
            default));

        Assert.Equal("ANALYSIS_SUMMARY_TOO_LONG", error.Code);
        Assert.Equal("기존 제목", content.Title);
        Assert.Equal(WorkflowStep.URL, content.CurrentWorkflowStep);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Save_RejectsIncompleteManualIntakeWithoutMutation()
    {
        var content = CreateContent();
        content.IntakeStatus = IntakeStatus.MANUAL_INPUT_REQUIRED;
        var repository = new Repository(content);
        var service = new AnalysisReviewService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new("변경 시도", "요약", true, RowVersion(content)),
            default));

        Assert.Equal("MANUAL_INSTAGRAM_INTAKE_NOT_READY", error.Code);
        Assert.Equal("기존 제목", content.Title);
        Assert.Equal(WorkflowStep.URL, content.CurrentWorkflowStep);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Save_RejectsCompletedReviewWithoutChangingContent()
    {
        var content = CreateContent();
        content.CurrentWorkflowStep = WorkflowStep.CATEGORY_EDIT;
        var repository = new Repository(content);
        var service = new AnalysisReviewService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new("변경 시도", "요약", false, RowVersion(content)),
            default));

        Assert.Equal("ANALYSIS_REVIEW_ALREADY_COMPLETED", error.Code);
        Assert.Equal("기존 제목", content.Title);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData(SourceAcquisitionMode.MANUAL)]
    [InlineData(SourceAcquisitionMode.HTTP_METADATA)]
    public async Task Save_AllowsReadyGenericTextIntake(SourceAcquisitionMode acquisitionMode)
    {
        var content = CreateContent();
        content.SourceKind = ContentSourceKind.GENERIC;
        content.InstagramContentType = null;
        content.ManualCaption = null;
        content.PinnedAuthorCommentState = null;
        content.SourceAcquisitionMode = acquisitionMode;
        content.DetailContent = "준비된 일반 URL 본문";
        content.IntakeStatus = IntakeStatus.CONTENT_READY;
        var repository = new Repository(content);
        var service = new AnalysisReviewService(repository);

        var result = await service.ExecuteAsync(
            content.Id,
            new("일반 URL 제목", "일반 URL 요약", true, RowVersion(content)),
            default);

        Assert.Equal(WorkflowStep.CATEGORY_EDIT, result.CurrentWorkflowStep);
        Assert.Equal(1, repository.SaveCount);
    }

    private static Content CreateContent() => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "기존 제목",
        RowVersion = [1, 2, 3],
        SourceKind = ContentSourceKind.INSTAGRAM,
        InstagramContentType = InstagramContentType.POST,
        ManualCaption = "수동 Caption",
        PinnedAuthorCommentState = PinnedAuthorCommentState.NONE,
        SourceAcquisitionMode = SourceAcquisitionMode.MANUAL,
        IntakeStatus = IntakeStatus.CONTENT_READY
    };

    private static string RowVersion(Content content) => Convert.ToBase64String(content.RowVersion);

    private sealed class Repository(Content content) : IContentRepository
    {
        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult(id == content.Id ? content : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ContentSummary>([], 0, query.Page, query.PageSize));

        public Task AddAsync(Content value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult<Tag?>(null);
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
