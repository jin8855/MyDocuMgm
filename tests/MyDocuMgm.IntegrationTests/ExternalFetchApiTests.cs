using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;
using MyDocuMgm.Application.ExternalFetch;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.Data;
using Xunit.Abstractions;

namespace MyDocuMgm.IntegrationTests;

public sealed class ExternalFetchApiTests : IDisposable
{
    private readonly Repository _repository = new();
    private readonly PageFetcher _fetcher = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ExternalFetchApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalFetchRepository>();
                services.RemoveAll<IExternalPageFetcher>();
                services.AddSingleton<IExternalFetchRepository>(_repository);
                services.AddSingleton<IExternalPageFetcher>(_fetcher);
            });
        });
    }

    [Fact]
    public async Task PreviewAndExplicitApply_AreSeparateHttpOperations()
    {
        using var client = _factory.CreateClient();

        using var previewResponse = await client.PostAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches",
            null);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ExternalFetchAttemptDto>();

        Assert.Equal(HttpStatusCode.Created, previewResponse.StatusCode);
        Assert.Equal("SUCCEEDED", preview!.Status);
        Assert.Equal("Before fetch", _repository.Content.Title);
        Assert.Equal(IntakeStatus.URL_ACCEPTED, _repository.Content.IntakeStatus);
        Assert.Empty(_repository.Evidence);

        using var applyResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches/{preview.Id}/apply",
            new
            {
                title = "Applied title",
                description = "Applied description",
                body = preview.Body
            });
        var applied = await applyResponse.Content.ReadFromJsonAsync<ExternalFetchApplyDto>();

        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
        Assert.Equal("APPLIED", applied!.Attempt.Status);
        Assert.Equal("CONTENT_READY", applied.Intake.Status);
        Assert.Equal("HTTP_METADATA", applied.Intake.SourceAcquisitionMode);
        Assert.Equal("Applied title", _repository.Content.Title);
        Assert.Single(_repository.Evidence);
        Assert.Equal(1, _fetcher.CallCount);
    }

    [Fact]
    public async Task LatestFetch_Uses204ForExistingEmptyContentAndPreserves200And404Contracts()
    {
        using var client = _factory.CreateClient();

        using var empty = await client.GetAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches/latest");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);

        using var previewResponse = await client.PostAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches",
            null);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ExternalFetchAttemptDto>();
        using var latest = await client.GetAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches/latest");
        var latestDto = await latest.Content.ReadFromJsonAsync<ExternalFetchAttemptDto>();
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.Equal(preview!.Id, latestDto!.Id);

        using var missing = await client.GetAsync(
            $"/api/url-intakes/{Guid.NewGuid()}/external-fetches/latest");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(201, 0, "EXTERNAL_FETCH_TITLE_TOO_LONG")]
    [InlineData(0, 501, "EXTERNAL_FETCH_DESCRIPTION_TOO_LONG")]
    public async Task ApplyFetch_RejectsExplicitMetadataOverLimitAsValidation(
        int titleLength,
        int descriptionLength,
        string expectedCode)
    {
        using var client = _factory.CreateClient();
        using var previewResponse = await client.PostAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches",
            null);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ExternalFetchAttemptDto>();

        using var response = await client.PutAsJsonAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches/{preview!.Id}/apply",
            new
            {
                title = titleLength == 0 ? null : new string('T', titleLength),
                description = descriptionLength == 0 ? null : new string('D', descriptionLength),
                body = preview.Body
            });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, problem!["code"].ToString());
        Assert.Equal("Before fetch", _repository.Content.Title);
        Assert.Equal(IntakeStatus.URL_ACCEPTED, _repository.Content.IntakeStatus);
        Assert.Empty(_repository.Evidence);
    }

    [Fact]
    public async Task RemoteFailure_ReturnsBadGatewayAndLatestAttemptPreservesFailure()
    {
        _fetcher.Error = new ExternalFetchException(
            "FETCH_REMOTE_STATUS",
            "synthetic remote failure",
            ExternalFetchFailureKind.REMOTE);
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches",
            null);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var latest = await client.GetFromJsonAsync<ExternalFetchAttemptDto>(
            $"/api/url-intakes/{_repository.Content.Id}/external-fetches/latest");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("FETCH_REMOTE_STATUS", problem!["code"].ToString());
        Assert.Equal("FAILED", latest!.Status);
        Assert.Equal("FETCH_REMOTE_STATUS", latest.ErrorCode);
        Assert.Equal("Before fetch", _repository.Content.Title);
        Assert.Equal(IntakeStatus.URL_ACCEPTED, _repository.Content.IntakeStatus);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class PageFetcher : IExternalPageFetcher
    {
        public int CallCount { get; private set; }
        public Exception? Error { get; set; }

        public Task<ExternalPageResponse> FetchAsync(Uri uri, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Error is not null)
            {
                return Task.FromException<ExternalPageResponse>(Error);
            }

            const string html = """
                <html><head><title>Fetched title</title></head>
                <body><main><p>Fetched body</p></main></body></html>
                """;
            return Task.FromResult(new ExternalPageResponse(
                uri.AbsoluteUri,
                200,
                "text/html",
                html.Length,
                "SYNTHETIC_SHA256",
                null,
                null,
                html));
        }
    }

    private sealed class Repository : IExternalFetchRepository
    {
        public Content Content { get; } = new()
        {
            CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
            Title = "Before fetch",
            CurrentWorkflowStep = WorkflowStep.URL,
            OriginalUrl = "https://example.test/article",
            NormalizedUrl = "https://example.test/article",
            SourceKind = ContentSourceKind.GENERIC,
            IntakeStatus = IntakeStatus.URL_ACCEPTED,
            RowVersion = [1]
        };
        public List<ExternalFetchAttempt> Attempts { get; } = [];
        public List<SourceEvidence> Evidence { get; } = [];

        public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult(contentId == Content.Id ? Content : null);

        public Task<ExternalFetchAttempt> CreateAttemptAsync(
            Content content,
            DateTime recentCutoffUtc,
            CancellationToken cancellationToken)
        {
            var attempt = new ExternalFetchAttempt
            {
                ContentId = content.Id,
                Content = content,
                AttemptNumber = Attempts.Count + 1
            };
            Attempts.Add(attempt);
            return Task.FromResult(attempt);
        }

        public Task<ExternalFetchAttempt?> FindAttemptAsync(
            Guid contentId,
            Guid attemptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Attempts.SingleOrDefault(
                attempt => attempt.ContentId == contentId && attempt.Id == attemptId));

        public Task<ExternalFetchAttempt?> FindLatestAttemptAsync(
            Guid contentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Attempts.LastOrDefault(attempt => attempt.ContentId == contentId));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyAsync(
            Content content,
            ExternalFetchAttempt attempt,
            SourceEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            return Task.CompletedTask;
        }
    }
}

internal sealed class Phase2CRepairSqlFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "MYDOCUMGM_PHASE2C_REPAIR_SQL";

    public Phase2CRepairSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to an approved disposable SQL Server host.";
        }
    }
}

public sealed class Phase2CExternalFetchSqlTests(ITestOutputHelper output)
{
    [Phase2CRepairSqlFact]
    public async Task DisposableSqlServer_ConcurrentRetryLimitIsAtomicAndWindowExpires()
    {
        var configured = Environment.GetEnvironmentVariable(Phase2CRepairSqlFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("Approved disposable SQL Server setting is missing.");
        var databaseName = $"MyDocuMgm_P2C_Repair_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var template = new SqlConnectionStringBuilder(configured);
        if (!template.IntegratedSecurity ||
            !string.IsNullOrEmpty(template.UserID) ||
            !string.IsNullOrEmpty(template.Password) ||
            template.InitialCatalog.Equals("MyDocuMgm", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Windows-authenticated disposable SQL Server is allowed.");
        }

        var master = new SqlConnectionStringBuilder(template.ConnectionString) { InitialCatalog = "master" };
        var target = new SqlConnectionStringBuilder(template.ConnectionString) { InitialCatalog = databaseName };
        await EnsureMissingAsync(master.ConnectionString, databaseName);
        output.WriteLine($"Disposable database: {databaseName}");

        try
        {
            await ExecuteMasterAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}]");
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var content = new Content
            {
                CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
                Title = "Synthetic Phase 2C retry owner",
                CurrentWorkflowStep = WorkflowStep.URL,
                OriginalUrl = "https://example.test/phase2c-retry",
                NormalizedUrl = "https://example.test/phase2c-retry",
                SourceKind = ContentSourceKind.GENERIC,
                IntakeStatus = IntakeStatus.URL_ACCEPTED
            };
            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                seedContext.Contents.Add(content);
                await seedContext.SaveChangesAsync();
            }

            var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MyDocuMgm"] = target.ConnectionString
                    }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<MyDocuMgmDbContext>();
                    services.RemoveAll<DbContextOptions<MyDocuMgmDbContext>>();
                    services.AddDbContext<MyDocuMgmDbContext>(options =>
                        options.UseSqlServer(target.ConnectionString));
                    services.RemoveAll<IExternalPageFetcher>();
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<IExternalPageFetcher, SuccessfulPageFetcher>();
                    services.AddSingleton<TimeProvider>(clock);
                });
            });
            using var client = factory.CreateClient();
            var requestPath = $"/api/url-intakes/{content.Id}/external-fetches";
            var concurrent = Enumerable.Range(0, 4)
                .Select(_ => client.PostAsync(requestPath, null))
                .ToArray();
            var responses = await Task.WhenAll(concurrent);

            foreach (var response in responses)
            {
                output.WriteLine(
                    $"Concurrent response: {(int)response.StatusCode} {response.StatusCode}; " +
                    await response.Content.ReadAsStringAsync());
            }

            Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            var limited = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.TooManyRequests);
            var problem = await limited.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.Equal("FETCH_RETRY_LIMIT", problem!["code"].ToString());
            Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
            foreach (var response in responses)
            {
                response.Dispose();
            }

            DateTime latestStartedAtUtc;
            await using (var verificationContext = new MyDocuMgmDbContext(options))
            {
                var attempts = await verificationContext.ExternalFetchAttempts
                    .Where(attempt => attempt.ContentId == content.Id)
                    .OrderBy(attempt => attempt.AttemptNumber)
                    .ToListAsync();
                Assert.Equal(3, attempts.Count);
                Assert.Equal([1, 2, 3], attempts.Select(attempt => attempt.AttemptNumber));
                latestStartedAtUtc = attempts.Max(attempt => attempt.StartedAtUtc);
            }

            clock.Set(new DateTimeOffset(latestStartedAtUtc, TimeSpan.Zero)
                .AddMinutes(10)
                .AddSeconds(1));
            using var afterWindow = await client.PostAsync(requestPath, null);
            Assert.Equal(HttpStatusCode.Created, afterWindow.StatusCode);
            await using (var verificationContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(4, await verificationContext.ExternalFetchAttempts.CountAsync(
                    attempt => attempt.ContentId == content.Id));
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync(
                master.ConnectionString,
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
            await EnsureMissingAsync(master.ConnectionString, databaseName);
            output.WriteLine($"Disposable database removed: {databaseName}; DB_ID = NULL");
        }
    }

    [Phase2CRepairSqlFact]
    public async Task DisposableSqlServer_PermanentCleanupRemovesOwnedFetchRowsAndPreservesOtherContent()
    {
        var configured = Environment.GetEnvironmentVariable(Phase2CRepairSqlFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("Approved disposable SQL Server setting is missing.");
        var databaseName = $"MyDocuMgm_P2C_Cleanup_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var template = new SqlConnectionStringBuilder(configured);
        if (!template.IntegratedSecurity ||
            !string.IsNullOrEmpty(template.UserID) ||
            !string.IsNullOrEmpty(template.Password) ||
            template.InitialCatalog.Equals("MyDocuMgm", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Windows-authenticated disposable SQL Server is allowed.");
        }

        var master = new SqlConnectionStringBuilder(template.ConnectionString) { InitialCatalog = "master" };
        var target = new SqlConnectionStringBuilder(template.ConnectionString) { InitialCatalog = databaseName };
        await EnsureMissingAsync(master.ConnectionString, databaseName);
        output.WriteLine($"Disposable cleanup database: {databaseName}");

        try
        {
            await ExecuteMasterAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}]");
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var categoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id;
            var cleanupTarget = CreateGenericContent(categoryId, "Synthetic cleanup target", "cleanup-target");
            var survivor = CreateGenericContent(categoryId, "Synthetic survivor", "survivor");
            var ordinaryDeleted = CreateGenericContent(categoryId, "Synthetic ordinary deleted", "ordinary-deleted");
            ordinaryDeleted.SoftDelete();
            var survivorAttempt = new ExternalFetchAttempt
            {
                ContentId = survivor.Id,
                Content = survivor,
                AttemptNumber = 1
            };
            survivorAttempt.Succeed(
                survivor.NormalizedUrl!, 200, "text/html", 1, "SURVIVOR_SHA", null, null,
                "Survivor title", "Survivor description", null, null, "Survivor body");
            var survivorEvidence = new SourceEvidence
            {
                ContentId = survivor.Id,
                Content = survivor,
                SourceType = "PUBLIC_HTML",
                SourceTitle = "Survivor evidence",
                SourceReference = survivor.NormalizedUrl
            };

            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                seedContext.Contents.AddRange(cleanupTarget, survivor, ordinaryDeleted);
                seedContext.ExternalFetchAttempts.Add(survivorAttempt);
                seedContext.SourceEvidence.Add(survivorEvidence);
                await seedContext.SaveChangesAsync();
            }

            ExternalFetchAttemptDto preview;
            await using (var fetchContext = new MyDocuMgmDbContext(options))
            {
                var service = new ExternalFetchService(
                    new EfExternalFetchRepository(fetchContext),
                    new SuccessfulPageFetcher(),
                    new MyDocuMgm.Infrastructure.ExternalFetch.HtmlContentExtractor(),
                    TimeProvider.System);
                preview = await service.StartAsync(cleanupTarget.Id, default);
                await service.ApplyAsync(
                    cleanupTarget.Id,
                    preview.Id,
                    new(preview.Title, preview.Description, preview.Body!),
                    default);
                var trackedTarget = await fetchContext.Contents.SingleAsync(content => content.Id == cleanupTarget.Id);
                trackedTarget.SoftDelete();
                await fetchContext.SaveChangesAsync();
            }

            await using (var beforeContext = new MyDocuMgmDbContext(options))
            {
                var before = await OwnedCountsAsync(beforeContext, cleanupTarget.Id);
                output.WriteLine($"Before cleanup Content/Evidence/Attempt/Media = {before}");
                Assert.Equal((1, 1, 1, 0), before);
            }

            await using (var cleanupContext = new MyDocuMgmDbContext(options))
            {
                var cleanup = new CleanupService(
                    new EfCleanupRepository(cleanupContext),
                    new UnexpectedCleanupStorage(),
                    new NoopDiagnostics());
                await cleanup.PermanentlyDeleteContentAsync(cleanupTarget.Id, default);
                await cleanup.PermanentlyDeleteContentAsync(ordinaryDeleted.Id, default);
            }

            await using (var verificationContext = new MyDocuMgmDbContext(options))
            {
                var after = await OwnedCountsAsync(verificationContext, cleanupTarget.Id);
                output.WriteLine($"After cleanup Content/Evidence/Attempt/Media = {after}");
                Assert.Equal((0, 0, 0, 0), after);
                Assert.Equal(0, await verificationContext.Contents.IgnoreQueryFilters()
                    .CountAsync(content => content.Id == ordinaryDeleted.Id));
                Assert.Equal(1, await verificationContext.Contents.IgnoreQueryFilters()
                    .CountAsync(content => content.Id == survivor.Id));
                Assert.Equal(1, await verificationContext.SourceEvidence
                    .CountAsync(evidence => evidence.ContentId == survivor.Id));
                Assert.Equal(1, await verificationContext.ExternalFetchAttempts
                    .CountAsync(attempt => attempt.ContentId == survivor.Id));
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync(
                master.ConnectionString,
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
            await EnsureMissingAsync(master.ConnectionString, databaseName);
            output.WriteLine($"Disposable cleanup database removed: {databaseName}; DB_ID = NULL");
        }
    }

    private static Content CreateGenericContent(Guid categoryId, string title, string slug) => new()
    {
        CategoryId = categoryId,
        Title = title,
        CurrentWorkflowStep = WorkflowStep.URL,
        OriginalUrl = $"https://example.test/{slug}",
        NormalizedUrl = $"https://example.test/{slug}",
        SourceKind = ContentSourceKind.GENERIC,
        IntakeStatus = IntakeStatus.URL_ACCEPTED
    };

    private static async Task<(int Content, int Evidence, int Attempt, int Media)> OwnedCountsAsync(
        MyDocuMgmDbContext context,
        Guid contentId) =>
        (
            await context.Contents.IgnoreQueryFilters().CountAsync(content => content.Id == contentId),
            await context.SourceEvidence.CountAsync(evidence => evidence.ContentId == contentId),
            await context.ExternalFetchAttempts.CountAsync(attempt => attempt.ContentId == contentId),
            await context.MediaAssets.IgnoreQueryFilters().CountAsync(media => media.ContentId == contentId)
        );

    private static async Task EnsureMissingAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@databaseName)";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteMasterAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class SuccessfulPageFetcher : IExternalPageFetcher
    {
        public Task<ExternalPageResponse> FetchAsync(Uri uri, CancellationToken cancellationToken)
        {
            const string html = "<html><body><main><p>Synthetic Phase 2C body</p></main></body></html>";
            return Task.FromResult(new ExternalPageResponse(
                uri.AbsoluteUri,
                200,
                "text/html",
                html.Length,
                "SYNTHETIC_PHASE2C_SHA256",
                null,
                null,
                html));
        }
    }

    private sealed class UnexpectedCleanupStorage : MyDocuMgm.Application.IMediaCleanupStorage
    {
        private static InvalidOperationException Unexpected() =>
            new("Content cleanup must not access managed media storage when owned media count is zero.");

        public Task<MyDocuMgm.Application.MediaCleanupStorageState> InspectAsync(
            Guid contentId, Guid mediaId, string relativePath, string storedFileName,
            CancellationToken cancellationToken) => throw Unexpected();
        public Task<MyDocuMgm.Application.PreparedMediaCleanup> PrepareDeleteAsync(
            Guid contentId, Guid mediaId, string relativePath, string storedFileName,
            CancellationToken cancellationToken) => throw Unexpected();
        public Task RestoreAsync(
            MyDocuMgm.Application.PreparedMediaCleanup cleanup,
            CancellationToken cancellationToken) => throw Unexpected();
        public Task CommitAsync(
            MyDocuMgm.Application.PreparedMediaCleanup cleanup,
            CancellationToken cancellationToken) => throw Unexpected();
    }

    private sealed class NoopDiagnostics : MyDocuMgm.Application.IMediaDiagnostics
    {
        public void Record(string code, Guid? contentId, Guid? mediaId = null) { }
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;

        public override DateTimeOffset GetUtcNow() => _value;

        public void Set(DateTimeOffset value) => _value = value;
    }
}
