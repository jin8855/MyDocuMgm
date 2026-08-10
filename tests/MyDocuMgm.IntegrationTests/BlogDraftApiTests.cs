using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application.Contents.WriteBlogDraft;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class BlogDraftApiTests : IDisposable
{
    private readonly Content _content = new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "합성 분석 제목",
        ShortSummary = "합성 요약 fallback",
        DetailContent = "합성 직접 입력 본문",
        CurrentWorkflowStep = WorkflowStep.BLOG_DRAFT,
        RowVersion = [7, 8, 9],
        OtherDetails = new OtherDetails { CustomLabel = "합성 분류" }
    };
    private readonly Repository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public BlogDraftApiTests()
    {
        _repository = new Repository(_content);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBlogDraftRepository>();
                services.AddSingleton<IBlogDraftRepository>(_repository);
            });
        });
    }

    [Fact]
    public async Task GetAndPartialSaves_UseFallbackThenRestoreOneSavedDraft()
    {
        using var client = _factory.CreateClient();

        using var loaded = await client.GetAsync($"/api/contents/{_content.Id}/blog-draft");
        var initial = await loaded.Content.ReadFromJsonAsync<BlogDraftDto>();
        using var titleSaved = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            title = "저장 제목",
            complete = false,
            contentRowVersion = initial!.ContentRowVersion
        });
        var first = await titleSaved.Content.ReadFromJsonAsync<BlogDraftDto>();
        using var bodySaved = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            body = "저장 본문\n둘째 줄",
            complete = false,
            contentRowVersion = first!.ContentRowVersion,
            draftRowVersion = first.DraftRowVersion
        });
        var second = await bodySaved.Content.ReadFromJsonAsync<BlogDraftDto>();

        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal("합성 분석 제목", initial.Title);
        Assert.Equal("합성 직접 입력 본문", initial.Body);
        Assert.False(initial.HasSavedDraft);
        Assert.Equal(HttpStatusCode.OK, titleSaved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bodySaved.StatusCode);
        Assert.Equal("저장 제목", second!.Title);
        Assert.Equal("저장 본문\n둘째 줄", second.Body);
        Assert.Equal("합성 분석 제목", _content.Title);
        Assert.Equal(1, _repository.AddCount);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task EntryBypassAndForeignContentAreRejectedWithoutWrite()
    {
        using var client = _factory.CreateClient();
        _content.CurrentWorkflowStep = WorkflowStep.DETAIL;

        using var bypass = await client.GetAsync($"/api/contents/{_content.Id}/blog-draft");
        using var foreign = await client.GetAsync($"/api/contents/{Guid.NewGuid()}/blog-draft");

        Assert.Equal(HttpStatusCode.BadRequest, bypass.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Complete_ValidatesThenMovesToCompletedAndRetryIsIdempotent()
    {
        using var client = _factory.CreateClient();
        using var invalid = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            title = " ",
            body = "본문",
            complete = true,
            contentRowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        using var completed = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            title = "완료 제목",
            body = "완료 본문",
            complete = true,
            contentRowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        using var retry = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            title = "완료 제목",
            body = "완료 본문",
            complete = true,
            contentRowVersion = "lost-response-version",
            draftRowVersion = "lost-response-version"
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(WorkflowStep.COMPLETED, _content.CurrentWorkflowStep);
        Assert.Equal(1, _repository.AddCount);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task CompletionGet_ReturnsSavedReadOnlyProjectionWithoutWrite()
    {
        _content.CurrentWorkflowStep = WorkflowStep.COMPLETED;
        _content.BlogDraft = new BlogDraft
        {
            ContentId = _content.Id,
            Content = _content,
            Title = "저장된 완료 제목",
            Body = "첫 줄\n둘째 줄",
            RowVersion = [11, 12, 13]
        };
        using var client = _factory.CreateClient();

        using var first = await client.GetAsync($"/api/contents/{_content.Id}/completion");
        using var second = await client.GetAsync($"/api/contents/{_content.Id}/completion");
        var result = await second.Content.ReadFromJsonAsync<BlogDraftDto>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(WorkflowStep.COMPLETED, result!.CurrentWorkflowStep);
        Assert.Equal("저장된 완료 제목", result.Title);
        Assert.Equal("첫 줄\n둘째 줄", result.Body);
        Assert.Equal(0, _repository.AddCount);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task CompletionGet_RejectsIncompleteMissingDraftAndForeignContentWithoutWrite()
    {
        using var client = _factory.CreateClient();
        using var incomplete = await client.GetAsync($"/api/contents/{_content.Id}/completion");
        _content.CurrentWorkflowStep = WorkflowStep.COMPLETED;
        using var missingDraft = await client.GetAsync($"/api/contents/{_content.Id}/completion");
        using var foreign = await client.GetAsync($"/api/contents/{Guid.NewGuid()}/completion");

        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingDraft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, _repository.AddCount);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task SchemaLengthOverflow_ReturnsValidationErrorNotServerError()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/blog-draft", new
        {
            title = new string('가', BlogDraft.TitleMaxLength + 1),
            body = "본문",
            complete = false,
            contentRowVersion = Convert.ToBase64String(_content.RowVersion)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _repository.SaveCount);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class Repository(Content content) : IBlogDraftRepository
    {
        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(contentId == content.Id ? content : null);

        public void Add(BlogDraft draft) => AddCount++;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (content.BlogDraft is { } draft) draft.RowVersion = [(byte)(20 + SaveCount)];
            return Task.CompletedTask;
        }
    }
}
