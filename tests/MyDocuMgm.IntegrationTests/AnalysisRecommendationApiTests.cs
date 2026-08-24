using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;
using MyDocuMgm.Application.AnalysisRecommendations;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class AnalysisRecommendationApiTests
{
    [Fact]
    public async Task ManualPromptAndImport_UseStrictHttpContract_AndDoNotCallProviderOrMutateContent()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        using var factory = CreateFactory(repository, provider: null);
        using var client = factory.CreateClient();

        using var promptResponse = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations/manual-prompt",
            new { includedEvidenceKinds = new[] { "CURRENT_TITLE", "CURRENT_SUMMARY", "DETAIL_CONTENT", "CURRENT_CATEGORY" } });
        Assert.Equal(HttpStatusCode.OK, promptResponse.StatusCode);
        var prompt = (await promptResponse.Content.ReadFromJsonAsync<ManualRecommendationPromptDto>())!;
        Assert.Contains("외부 URL을 추가로 탐색하지 마세요", prompt.Prompt);
        Assert.Empty(repository.Runs);

        var raw = JsonSerializer.Serialize(new
        {
            schemaVersion = prompt.SchemaVersion,
            sourceFingerprint = prompt.SourceFingerprint,
            recommendations = new
            {
                title = new { value = "HTTP 추천 제목", reason = "E1 근거", confidence = "HIGH", evidenceIds = new[] { "E1" } },
                summary = (object?)null,
                category = new { value = "OTHER", reason = "E1 근거", confidence = "LOW", evidenceIds = new[] { "E1" } },
                tags = (object?)null
            }
        });
        using var importResponse = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations/manual-import",
            new
            {
                schemaVersion = prompt.SchemaVersion,
                sourceFingerprint = prompt.SourceFingerprint,
                pastedResponse = raw,
                idempotencyKey = "manual-http-import",
                includedEvidenceIds = prompt.Evidence.Select(value => value.EvidenceId).ToArray()
            });
        var run = await importResponse.Content.ReadFromJsonAsync<AnalysisRecommendationRunDto>();

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.Equal(ManualAnalysisRecommendationService.Provenance, run!.ProviderIdentifier);
        Assert.Equal(AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED, run.Status);
        Assert.Equal("기존 제목", content.Title);
        Assert.Single(repository.Runs);

        using var invalidResponse = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations/manual-import",
            new
            {
                schemaVersion = prompt.SchemaVersion,
                sourceFingerprint = prompt.SourceFingerprint,
                pastedResponse = "not json",
                idempotencyKey = "manual-http-invalid",
                includedEvidenceIds = prompt.Evidence.Select(value => value.EvidenceId).ToArray()
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Single(repository.Runs);
    }

    [Fact]
    public async Task DefaultProvider_FailsClosedWithSafeProblem_AndPersistsFailedAudit()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        using var factory = CreateFactory(repository, provider: null);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations",
            new { idempotencyKey = "http-unavailable" });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("code", problem!);
        Assert.Equal(AnalysisRecommendationRunStatus.FAILED, repository.Runs.Single().Status);
        Assert.Equal("기존 제목", content.Title);
    }

    [Fact]
    public async Task RequestLatestAndDecide_UseStringEnumContract_AndPreserveRejectedValue()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        using var factory = CreateFactory(repository, new Provider(content));
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations",
            new { idempotencyKey = "http-success" });
        var run = await createResponse.Content.ReadFromJsonAsync<AnalysisRecommendationRunDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal(AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED, run!.Status);
        Assert.Equal("TITLE", await ReadJsonPropertyAsync(createResponse, "items", 0, "kind"));

        using var latestResponse = await client.GetAsync($"/api/contents/{content.Id}/analysis-recommendations/latest");
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);

        var title = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.TITLE);
        var category = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.CATEGORY);
        using var decideResponse = await client.PutAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations/{run.Id}/decisions",
            new
            {
                contentRowVersion = Convert.ToBase64String(content.RowVersion),
                decisions = new object[]
                {
                    new { itemId = title.Id, decision = "MODIFIED", modifiedValue = "사용자 확정 제목" },
                    new { itemId = category.Id, decision = "REJECTED", modifiedValue = (string?)null }
                }
            });
        var result = await decideResponse.Content.ReadFromJsonAsync<AnalysisRecommendationDecisionResult>();

        Assert.Equal(HttpStatusCode.OK, decideResponse.StatusCode);
        Assert.Equal("사용자 확정 제목", result!.Content.Title);
        Assert.Equal(content.CategoryId, result.Content.CategoryId);
        Assert.Equal(AnalysisRecommendationDecision.REJECTED,
            result.Run.Items.Single(value => value.Id == category.Id).Decision);
    }

    [Fact]
    public async Task DecisionConcurrencyConflict_Returns409WithoutPersistingAnyDecision()
    {
        var content = CreateContent();
        var repository = new Repository(content);
        using var factory = CreateFactory(repository, new Provider(content));
        using var client = factory.CreateClient();
        var run = await (await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations",
            new { idempotencyKey = "http-conflict" })).Content.ReadFromJsonAsync<AnalysisRecommendationRunDto>();
        var title = run!.Items.Single(value => value.Kind == AnalysisRecommendationKind.TITLE);

        using var response = await client.PutAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations/{run.Id}/decisions",
            new
            {
                contentRowVersion = Convert.ToBase64String([9, 9, 9]),
                decisions = new[] { new { itemId = title.Id, decision = "APPLIED", modifiedValue = (string?)null } }
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("기존 제목", content.Title);
        Assert.All(repository.Runs.Single().Items, value => Assert.Equal(AnalysisRecommendationDecision.PENDING, value.Decision));
    }

    private static async Task<string?> ReadJsonPropertyAsync(
        HttpResponseMessage response,
        string arrayProperty,
        int index,
        string property)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        return document.RootElement.GetProperty(arrayProperty)[index].GetProperty(property).GetString();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Repository repository,
        IAnalysisRecommendationProvider? provider) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnalysisRecommendationRepository>();
                services.AddSingleton<IAnalysisRecommendationRepository>(repository);
                if (provider is not null)
                {
                    services.RemoveAll<IAnalysisRecommendationProvider>();
                    services.AddSingleton(provider);
                }
            });
        });

    private static Content CreateContent()
    {
        var definition = CategoryCatalog.All.Single(value => value.Code == "OTHER");
        return new Content
        {
            CategoryId = definition.Id,
            Category = new Category { Id = definition.Id, Code = definition.Code, DisplayName = definition.DisplayName },
            Title = "기존 제목",
            ShortSummary = "기존 요약",
            DetailContent = "현재 본문",
            SourceKind = ContentSourceKind.GENERIC,
            SourceAcquisitionMode = SourceAcquisitionMode.MANUAL,
            IntakeStatus = IntakeStatus.CONTENT_READY,
            CurrentWorkflowStep = WorkflowStep.ANALYSIS_REVIEW,
            RowVersion = [1, 2, 3]
        };
    }

    private sealed class Provider(Content content) : IAnalysisRecommendationProvider
    {
        public string ProviderIdentifier => "integration-fake";
        public Task<AnalysisRecommendationProviderResult> RecommendAsync(
            AnalysisRecommendationProviderInput input,
            CancellationToken cancellationToken) => Task.FromResult(new AnalysisRecommendationProviderResult(
                false,
                "fake-v1",
                [
                    new(AnalysisRecommendationKind.TITLE, "추천 제목", "본문 근거", AnalysisRecommendationConfidence.HIGH, []),
                    new(AnalysisRecommendationKind.CATEGORY, content.CategoryId.ToString(), "현재 분류 근거", AnalysisRecommendationConfidence.LOW, [])
                ]));
    }

    private sealed class Repository(Content content) : IAnalysisRecommendationRepository
    {
        public List<AnalysisRecommendationRun> Runs { get; } = [];
        public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) => Task.FromResult(contentId == content.Id ? content : null);
        public Task<AnalysisRecommendationRun?> FindByIdempotencyKeyAsync(Guid contentId, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Runs.SingleOrDefault(value => value.ContentId == contentId && value.IdempotencyKey == idempotencyKey));
        public Task<AnalysisRecommendationRun?> FindLatestAsync(Guid contentId, CancellationToken cancellationToken) => Task.FromResult(Runs.Where(value => value.ContentId == contentId).OrderByDescending(value => value.RequestedAtUtc).FirstOrDefault());
        public Task<IReadOnlyList<AnalysisRecommendationRun>> ListAsync(Guid contentId, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AnalysisRecommendationRun>>(Runs.Where(value => value.ContentId == contentId).Take(limit).ToArray());
        public Task<AnalysisRecommendationRun?> FindRunForDecisionAsync(Guid contentId, Guid runId, CancellationToken cancellationToken) => Task.FromResult(Runs.SingleOrDefault(value => value.ContentId == contentId && value.Id == runId));
        public Task AddRunAsync(AnalysisRecommendationRun run, CancellationToken cancellationToken) { Runs.Add(run); return Task.CompletedTask; }
        public Task AddItemsAsync(IReadOnlyCollection<AnalysisRecommendationItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult<Tag?>(null);
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
