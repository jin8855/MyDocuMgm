using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application.AnalysisRecommendations;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure;
using MyDocuMgm.Infrastructure.Data;
using Xunit;

namespace MyDocuMgm.IntegrationTests;

internal sealed class Phase2DRecommendationSqlFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "MYDOCUMGM_PHASE2D_SQL_UAT_CONNECTION";

    public Phase2DRecommendationSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to an approved disposable MyDocuMgm_P2D_Recommendation_ database.";
        }
    }
}

public sealed class SqlServerAnalysisRecommendationPersistenceTests
{
    [Phase2DRecommendationSqlFact]
    public async Task DisposableSqlServer_DefaultProviderFailsClosedAndPreservesContent()
    {
        var connection = Environment.GetEnvironmentVariable(Phase2DRecommendationSqlFactAttribute.ConnectionVariable)!;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<MyDocuMgmDbContext>();
                services.RemoveAll<DbContextOptions<MyDocuMgmDbContext>>();
                services.AddDbContext<MyDocuMgmDbContext>(options => options.UseSqlServer(connection));
            });
        });
        Assert.IsType<UnavailableAnalysisRecommendationProvider>(factory.Services.GetRequiredService<IAnalysisRecommendationProvider>());
        var categoryId = CategoryCatalog.All.Single(value => value.Code == "OTHER").Id;
        var content = CreateContent(Guid.NewGuid(), categoryId, "미연결 기존 제목");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            db.Contents.Add(content);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/api/contents/{content.Id}/analysis-recommendations",
            new { idempotencyKey = "sql-default-fail-closed" });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("code", problem!);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            var stored = await db.Contents.AsNoTracking().SingleAsync(value => value.Id == content.Id);
            Assert.Equal("미연결 기존 제목", stored.Title);
            Assert.Equal(AnalysisRecommendationRunStatus.FAILED,
                (await db.AnalysisRecommendationRuns.SingleAsync(value => value.ContentId == content.Id)).Status);
            db.Contents.Remove(await db.Contents.SingleAsync(value => value.Id == content.Id));
            await db.SaveChangesAsync();
        }
    }

    [Phase2DRecommendationSqlFact]
    public async Task DisposableSqlServer_ModelRoundTripsRecommendationGraph()
    {
        var connection = Environment.GetEnvironmentVariable(Phase2DRecommendationSqlFactAttribute.ConnectionVariable)!;
        var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>().UseSqlServer(connection).Options;
        await using var db = new MyDocuMgmDbContext(options);
        var categoryId = CategoryCatalog.All.Single(value => value.Code == "OTHER").Id;
        var content = CreateContent(Guid.NewGuid(), categoryId, "그래프 테스트");
        var run = new AnalysisRecommendationRun
        {
            Content = content, ContentId = content.Id, ProviderIdentifier = "sql-graph",
            IdempotencyKey = "sql-graph"
        };
        db.Add(run);
        await db.SaveChangesAsync();
        var storedAfterFirstSave = await db.AnalysisRecommendationRuns.AsNoTracking().SingleAsync(value => value.Id == run.Id);
        Assert.Equal(Convert.ToHexString(storedAfterFirstSave.RowVersion), Convert.ToHexString(run.RowVersion));
        var item = new AnalysisRecommendationItem
        {
            Run = run, RunId = run.Id, Kind = AnalysisRecommendationKind.TITLE,
            RecommendedValue = "그래프 추천", Reason = "그래프 근거", Confidence = AnalysisRecommendationConfidence.HIGH
        };
        item.Evidence.Add(new AnalysisRecommendationEvidence
        {
            Item = item, ItemId = item.Id, EvidenceType = AnalysisRecommendationEvidenceType.DETAIL_CONTENT,
            Excerpt = "제한 근거"
        });
        run.Items.Add(item);
        db.AnalysisRecommendationItems.Add(item);
        run.Complete(false);

        await db.SaveChangesAsync();

        Assert.Equal(1, await db.AnalysisRecommendationRuns.CountAsync(value => value.Id == run.Id));
        Assert.Equal(1, await db.AnalysisRecommendationItems.CountAsync(value => value.RunId == run.Id));
        Assert.Equal(1, await db.AnalysisRecommendationEvidenceRecords.CountAsync(value => value.ItemId == item.Id));
        db.Contents.Remove(content);
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.AnalysisRecommendationRuns.CountAsync(value => value.Id == run.Id));
    }

    [Phase2DRecommendationSqlFact]
    public async Task DisposableSqlServer_PersistsAtomicDecisionsIdempotencyIsolationAndCascade()
    {
        var connection = Environment.GetEnvironmentVariable(Phase2DRecommendationSqlFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("Disposable SQL connection is required.");
        Assert.Contains("Database=MyDocuMgm_P2D_Recommendation_", connection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=MyDocuMgm;", connection, StringComparison.OrdinalIgnoreCase);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<MyDocuMgmDbContext>();
                services.RemoveAll<DbContextOptions<MyDocuMgmDbContext>>();
                services.AddDbContext<MyDocuMgmDbContext>(options => options.UseSqlServer(connection));
                services.RemoveAll<IAnalysisRecommendationProvider>();
                services.AddSingleton<IAnalysisRecommendationProvider, SqlFakeProvider>();
            });
        });

        var contentId = Guid.NewGuid();
        var otherContentId = Guid.NewGuid();
        var categoryId = CategoryCatalog.All.Single(value => value.Code == "OTHER").Id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            db.Contents.AddRange(
                CreateContent(contentId, categoryId, "SQL 기존 제목"),
                CreateContent(otherContentId, categoryId, "다른 SQL 자료"));
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        AnalysisRecommendationRunDto run;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            var before = await db.Contents.AsNoTracking().SingleAsync(value => value.Id == contentId);
            using var response = await client.PostAsJsonAsync(
                $"/api/contents/{contentId}/analysis-recommendations",
                new { idempotencyKey = "sql-uat-request" });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            run = (await response.Content.ReadFromJsonAsync<AnalysisRecommendationRunDto>())!;

            var after = await db.Contents.AsNoTracking().SingleAsync(value => value.Id == contentId);
            Assert.Equal(before.Title, after.Title);
            Assert.Equal(before.ShortSummary, after.ShortSummary);
            Assert.Equal(1, await db.AnalysisRecommendationRuns.CountAsync(value => value.ContentId == contentId));
            Assert.Equal(4, await db.AnalysisRecommendationItems.CountAsync(value => value.RunId == run.Id));
        }

        var title = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.TITLE);
        var summary = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.SUMMARY);
        var category = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.CATEGORY);
        var tag = run.Items.Single(value => value.Kind == AnalysisRecommendationKind.TAG);
        string contentRowVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            contentRowVersion = Convert.ToBase64String((await db.Contents.AsNoTracking().SingleAsync(value => value.Id == contentId)).RowVersion);
        }
        var decisions = new
        {
            contentRowVersion,
            decisions = new object[]
            {
                new { itemId = title.Id, decision = "APPLIED", modifiedValue = (string?)null },
                new { itemId = summary.Id, decision = "MODIFIED", modifiedValue = "SQL 사용자 요약" },
                new { itemId = category.Id, decision = "REJECTED", modifiedValue = (string?)null },
                new { itemId = tag.Id, decision = "APPLIED", modifiedValue = (string?)null }
            }
        };
        using var decideResponse = await client.PutAsJsonAsync(
            $"/api/contents/{contentId}/analysis-recommendations/{run.Id}/decisions", decisions);
        Assert.Equal(HttpStatusCode.OK, decideResponse.StatusCode);
        using var replayResponse = await client.PutAsJsonAsync(
            $"/api/contents/{contentId}/analysis-recommendations/{run.Id}/decisions", decisions);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            var stored = await db.Contents.AsNoTracking().SingleAsync(value => value.Id == contentId);
            Assert.Equal("SQL 추천 제목", stored.Title);
            Assert.Equal("SQL 사용자 요약", stored.ShortSummary);
            Assert.Equal(categoryId, stored.CategoryId);
            Assert.Equal(1, await db.ContentTags.CountAsync(value => value.ContentId == contentId));
            Assert.Equal(4, await db.AnalysisRecommendationItems.CountAsync(value => value.RunId == run.Id && value.Decision != AnalysisRecommendationDecision.PENDING));
        }

        using var mixedResponse = await client.PutAsJsonAsync(
            $"/api/contents/{otherContentId}/analysis-recommendations/{run.Id}/decisions", decisions);
        Assert.Equal(HttpStatusCode.NotFound, mixedResponse.StatusCode);

        using var secondRunResponse = await client.PostAsJsonAsync(
            $"/api/contents/{contentId}/analysis-recommendations", new { idempotencyKey = "sql-uat-conflict" });
        var secondRun = (await secondRunResponse.Content.ReadFromJsonAsync<AnalysisRecommendationRunDto>())!;
        var secondTitle = secondRun.Items.Single(value => value.Kind == AnalysisRecommendationKind.TITLE);
        using var conflictResponse = await client.PutAsJsonAsync(
            $"/api/contents/{contentId}/analysis-recommendations/{secondRun.Id}/decisions",
            new
            {
                contentRowVersion = Convert.ToBase64String([9, 9, 9]),
                decisions = new[] { new { itemId = secondTitle.Id, decision = "APPLIED", modifiedValue = (string?)null } }
            });
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDocuMgmDbContext>();
            db.Contents.RemoveRange(await db.Contents.IgnoreQueryFilters()
                .Where(value => value.Id == contentId || value.Id == otherContentId).ToArrayAsync());
            await db.SaveChangesAsync();
            Assert.Equal(0, await db.AnalysisRecommendationRuns.CountAsync(value => value.ContentId == contentId || value.ContentId == otherContentId));
            Assert.Equal(0, await db.AnalysisRecommendationItems.CountAsync(value => value.Run.ContentId == contentId || value.Run.ContentId == otherContentId));
        }
    }

    private static Content CreateContent(Guid id, Guid categoryId, string title) => new()
    {
        Id = id,
        CategoryId = categoryId,
        Title = title,
        ShortSummary = "SQL 기존 요약",
        DetailContent = "SQL 현재 본문",
        SourceKind = ContentSourceKind.GENERIC,
        SourceAcquisitionMode = SourceAcquisitionMode.MANUAL,
        IntakeStatus = IntakeStatus.CONTENT_READY,
        CurrentWorkflowStep = WorkflowStep.ANALYSIS_REVIEW
    };

    private sealed class SqlFakeProvider : IAnalysisRecommendationProvider
    {
        public string ProviderIdentifier => "sql-test-fake";
        public Task<AnalysisRecommendationProviderResult> RecommendAsync(
            AnalysisRecommendationProviderInput input,
            CancellationToken cancellationToken) => Task.FromResult(new AnalysisRecommendationProviderResult(
                false,
                "sql-fake-v1",
                [
                    new(AnalysisRecommendationKind.TITLE, "SQL 추천 제목", "현재 본문", AnalysisRecommendationConfidence.HIGH, input.Evidence.Take(1).ToArray()),
                    new(AnalysisRecommendationKind.SUMMARY, "SQL 추천 요약", "현재 본문", AnalysisRecommendationConfidence.MEDIUM, input.Evidence.Take(1).ToArray()),
                    new(AnalysisRecommendationKind.CATEGORY, input.CurrentCategoryId.ToString(), "현재 분류", AnalysisRecommendationConfidence.LOW, []),
                    new(AnalysisRecommendationKind.TAG, "SQL추천태그", "검색어", AnalysisRecommendationConfidence.MEDIUM, [])
                ]));
    }
}
