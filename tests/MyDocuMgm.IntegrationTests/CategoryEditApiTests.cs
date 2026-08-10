using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.EditCategory;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class CategoryEditApiTests : IDisposable
{
    private readonly Content _content = new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "분류 편집 API",
        ShortSummary = "검토 완료",
        CurrentWorkflowStep = WorkflowStep.CATEGORY_EDIT,
        RowVersion = [4, 5, 6]
    };
    private readonly Repository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public CategoryEditApiTests()
    {
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

    [Fact]
    public async Task DraftAndGet_RestoreCategoryValuesWithoutAdvancing()
    {
        using var client = _factory.CreateClient();
        var travel = CategoryCatalog.All.Single(category => category.Code == "TRAVEL");

        using var saved = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/category-edit", new
        {
            categoryId = travel.Id,
            values = new Dictionary<string, string?> { ["destination"] = "제주", ["transportation"] = "기차" },
            complete = false,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        using var loaded = await client.GetAsync($"/api/contents/{_content.Id}/category-edit");
        var result = await loaded.Content.ReadFromJsonAsync<CategoryEditDto>();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal(WorkflowStep.CATEGORY_EDIT, result!.CurrentWorkflowStep);
        Assert.Equal("제주", result.ValuesByCategory["TRAVEL"]["destination"]);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Complete_AdvancesToMediaAfterSingleSave()
    {
        using var client = _factory.CreateClient();
        var category = CategoryCatalog.All.Single(value => value.Code == "OTHER");

        using var response = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/category-edit", new
        {
            categoryId = category.Id,
            values = new Dictionary<string, string?> { ["customLabel"] = "생활 기록" },
            complete = true,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        var result = await response.Content.ReadFromJsonAsync<CategoryEditDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkflowStep.MEDIA, result!.CurrentWorkflowStep);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task DirectApiBypassBeforeAnalysisCompletion_IsRejected()
    {
        _content.CurrentWorkflowStep = WorkflowStep.ANALYSIS_REVIEW;
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/category-edit", new
        {
            categoryId = _content.CategoryId,
            values = new Dictionary<string, string?> { ["customLabel"] = "우회" },
            complete = true,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code", problem!);
        Assert.Equal(WorkflowStep.ANALYSIS_REVIEW, _content.CurrentWorkflowStep);
        Assert.Equal(0, _repository.SaveCount);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class Repository(Content content) : IContentRepository
    {
        public int SaveCount { get; private set; }
        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(id == content.Id ? content : null);
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
