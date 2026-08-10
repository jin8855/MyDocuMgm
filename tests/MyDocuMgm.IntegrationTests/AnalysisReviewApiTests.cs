using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class AnalysisReviewApiTests : IDisposable
{
    private readonly Content _content;
    private readonly Repository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public AnalysisReviewApiTests()
    {
        _content = new Content
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
        _repository = new Repository(_content);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContentRepository>();
                services.AddSingleton<IContentRepository>(_repository);
            });
        });
    }

    [Theory]
    [InlineData(false, "ANALYSIS_REVIEW")]
    [InlineData(true, "CATEGORY_EDIT")]
    public async Task Save_IsAtomicAndReturnsApprovedManualReview(bool complete, string expectedStep)
    {
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/contents/{_content.Id}/analysis-review",
            new
            {
                title = "  직접 작성한 제목  ",
                shortSummary = "  직접 작성한 요약  ",
                complete,
                rowVersion = Convert.ToBase64String(_content.RowVersion)
            });
        var result = await response.Content.ReadFromJsonAsync<ContentDetail>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("직접 작성한 제목", result!.Title);
        Assert.Equal("직접 작성한 요약", result.ShortSummary);
        Assert.Equal(expectedStep, result.CurrentWorkflowStep.ToString());
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task OverlongSummary_ReturnsValidationErrorWithoutSaving()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/contents/{_content.Id}/analysis-review",
            new
            {
                title = "제목",
                shortSummary = new string('가', 501),
                complete = false,
                rowVersion = Convert.ToBase64String(_content.RowVersion)
            });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code", problem!);
        Assert.Equal("기존 제목", _content.Title);
        Assert.Equal(WorkflowStep.URL, _content.CurrentWorkflowStep);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task IncompleteManualIntake_ReturnsValidationErrorWithoutSavingOrAdvancing()
    {
        _content.IntakeStatus = IntakeStatus.MANUAL_INPUT_REQUIRED;
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/contents/{_content.Id}/analysis-review",
            new
            {
                title = "변경 시도",
                shortSummary = "요약",
                complete = true,
                rowVersion = Convert.ToBase64String(_content.RowVersion)
            });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code", problem!);
        Assert.Equal("기존 제목", _content.Title);
        Assert.Equal(WorkflowStep.URL, _content.CurrentWorkflowStep);
        Assert.Equal(0, _repository.SaveCount);
    }

    public void Dispose() => _factory.Dispose();

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
