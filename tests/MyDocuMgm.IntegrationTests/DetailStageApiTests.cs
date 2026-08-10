using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application.Contents.ReviewDetail;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class DetailStageApiTests : IDisposable
{
    private readonly Content _content = new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "합성 DETAIL API",
        ShortSummary = "이전 단계 요약",
        DetailContent = "이전 단계 본문",
        CurrentWorkflowStep = WorkflowStep.DETAIL,
        RowVersion = [7, 8, 9],
        OtherDetails = new OtherDetails { CustomLabel = "합성 상세" }
    };
    private readonly Repository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public DetailStageApiTests()
    {
        _repository = new Repository(_content);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDetailStageRepository>();
                services.AddSingleton<IDetailStageRepository>(_repository);
            });
        });
    }

    [Fact]
    public async Task GetAndDraft_ReturnIntegratedDataAndKeepDetailWithoutWrite()
    {
        using var client = _factory.CreateClient();

        using var loaded = await client.GetAsync($"/api/contents/{_content.Id}/detail-stage");
        using var drafted = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/detail-stage", new
        {
            values = new Dictionary<string, string?>(),
            complete = false,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        var result = await drafted.Content.ReadFromJsonAsync<DetailStageDto>();

        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, drafted.StatusCode);
        Assert.Equal(WorkflowStep.DETAIL, result!.CurrentWorkflowStep);
        Assert.Equal("이전 단계 요약", result.ShortSummary);
        Assert.Equal("합성 상세", result.CategoryValues["customLabel"]);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Complete_AdvancesAtomicallyToBlogDraft()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/detail-stage", new
        {
            values = new Dictionary<string, string?>(),
            complete = true,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        var result = await response.Content.ReadFromJsonAsync<DetailStageDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkflowStep.BLOG_DRAFT, result!.CurrentWorkflowStep);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task EntryBypassAndUnapprovedFieldAreRejectedBeforeWrite()
    {
        using var client = _factory.CreateClient();
        using var field = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/detail-stage", new
        {
            values = new Dictionary<string, string?> { ["title"] = "덮어쓰기" },
            complete = false,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        _content.CurrentWorkflowStep = WorkflowStep.MEDIA;
        using var bypass = await client.GetAsync($"/api/contents/{_content.Id}/detail-stage");

        Assert.Equal(HttpStatusCode.BadRequest, field.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bypass.StatusCode);
        Assert.Equal("합성 DETAIL API", _content.Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class Repository(Content content) : IDetailStageRepository
    {
        public int SaveCount { get; private set; }
        public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(contentId == content.Id ? content : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
