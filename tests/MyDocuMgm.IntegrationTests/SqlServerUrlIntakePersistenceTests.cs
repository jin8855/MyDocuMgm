using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.Data;
using MyDocuMgm.Infrastructure.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace MyDocuMgm.IntegrationTests;

internal sealed class Phase2ASqlUatFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "MYDOCUMGM_PHASE2A_SQL_UAT";

    public Phase2ASqlUatFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to an approved disposable SQL Server database.";
        }
    }
}

public sealed class SqlServerUrlIntakePersistenceTests
{
    private const string ConnectionVariable = Phase2ASqlUatFactAttribute.ConnectionVariable;
    private const string SyntheticFailureCaption = "__PHASE2B_S1_TEST_FAILURE__";
    private readonly ITestOutputHelper _output;

    public SqlServerUrlIntakePersistenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Phase2ASqlUatFact]
    public async Task DisposableSqlServer_EnforcesConcurrentDuplicateAndUnlinkPreservesMediaAndFile()
    {
        var targetConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(targetConnection))
        {
            throw new InvalidOperationException($"{ConnectionVariable} was not available during SQL UAT execution.");
        }

        var target = new SqlConnectionStringBuilder(targetConnection);
        if (!string.Equals(target.DataSource, @"localhost\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase) ||
            !target.IntegratedSecurity ||
            !string.IsNullOrEmpty(target.UserID) ||
            !string.IsNullOrEmpty(target.Password) ||
            !Regex.IsMatch(target.InitialCatalog, "^MyDocuMgmPhase2A_Validation_[A-Za-z0-9_]+$"))
        {
            throw new InvalidOperationException("Phase 2A disposable SQL Server target is not approved or safe.");
        }

        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
        var mediaRoot = Path.Combine(Path.GetTempPath(), $"MyDocuMgm-phase2a-sql-media-{Guid.NewGuid():N}");
        await EnsureDatabaseMissingAsync(master.ConnectionString, databaseName);
        try
        {
            await CreateDatabaseAsync(master.ConnectionString, databaseName);
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var requests = Enumerable.Range(0, 20)
                .Select(index => IntakeAsync(
                    options,
                    $"https://WWW.INSTAGRAM.com/reel/SqlBoundary_42/?share={index}#fragment"))
                .ToArray();
            var results = await Task.WhenAll(requests);

            Assert.Single(results.Select(result => result.Id).Distinct());
            Assert.Single(results, result => result.IsDuplicate is false);
            Assert.Equal(19, results.Count(result => result.IsDuplicate));
            await using (var verificationContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(1, await verificationContext.Contents.CountAsync(
                    content => content.NormalizedUrlHash != null));
            }

            var mediaId = Guid.NewGuid();
            var bytes = "synthetic Phase 2A media boundary"u8.ToArray();
            var owner = new Content
            {
                CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
                Title = "Synthetic media owner"
            };
            var relativePath = $"media/{owner.Id:N}/{mediaId:N}/original/{mediaId:N}.png";
            var fullPath = Path.Combine(mediaRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, bytes);
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes));

            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                var media = new MediaAsset
                {
                    Id = mediaId,
                    ContentId = owner.Id,
                    OriginalFileName = "synthetic.png",
                    StoredFileName = $"{mediaId:N}.png",
                    RelativePath = relativePath,
                    MimeType = "image/png",
                    SizeBytes = bytes.Length,
                    Sha256 = expectedHash,
                    Width = 1,
                    Height = 1,
                    StorageStatus = MediaStorageStatus.READY
                };
                seedContext.Add(owner);
                seedContext.Add(media);
                await seedContext.SaveChangesAsync();
            }

            await using (var relationContext = new MyDocuMgmDbContext(options))
            {
                var repository = new EfUrlIntakeRepository(relationContext);
                var targetContent = await repository.FindAsync(results[0].Id, default);
                Assert.NotNull(targetContent);
                await repository.SaveManualInstagramAsync(
                    targetContent!,
                    "synthetic SQL caption",
                    PinnedAuthorCommentState.PRESENT,
                    "synthetic author pinned comment",
                    [mediaId],
                    default);
            }

            await using (var manualVerificationContext = new MyDocuMgmDbContext(options))
            {
                var service = new UrlIntakeService(new EfUrlIntakeRepository(manualVerificationContext));
                var reloaded = await service.GetAsync(results[0].Id, default);
                Assert.Equal("REEL", reloaded.InstagramContentType);
                Assert.Equal("synthetic SQL caption", reloaded.ManualCaption);
                Assert.Equal("PRESENT", reloaded.PinnedAuthorCommentState);
                Assert.Equal("synthetic author pinned comment", reloaded.PinnedAuthorCommentText);
                Assert.Equal("MANUAL", reloaded.SourceAcquisitionMode);
                Assert.Equal([mediaId], reloaded.LinkedMediaIds);

                await service.SaveManualInstagramAsync(
                    results[0].Id,
                    new("updated SQL caption", "NONE", "must be cleared", []),
                    default);
            }

            await using (var finalContext = new MyDocuMgmDbContext(options))
            {
                var preservedMedia = await finalContext.MediaAssets.SingleAsync(media => media.Id == mediaId);
                var persistedContent = await finalContext.Contents.SingleAsync(
                    content => content.Id == results[0].Id);
                Assert.Equal(relativePath, preservedMedia.RelativePath);
                Assert.Equal(PinnedAuthorCommentState.NONE, persistedContent.PinnedAuthorCommentState);
                Assert.Null(persistedContent.PinnedAuthorCommentText);
                Assert.Equal("updated SQL caption", persistedContent.ManualCaption);
                Assert.False(await finalContext.ContentMediaLinks.AnyAsync(link => link.MediaAssetId == mediaId));
            }
            Assert.True(File.Exists(fullPath));
            Assert.Equal(bytes.Length, new FileInfo(fullPath).Length);
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fullPath))));
        }
        finally
        {
            var cleanupErrors = new List<Exception>();
            if (Directory.Exists(mediaRoot))
            {
                try
                {
                    Directory.Delete(mediaRoot, recursive: true);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            SqlConnection.ClearAllPools();
            try
            {
                await DropDatabaseIfExistsAsync(master.ConnectionString, databaseName);
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException("Phase 2A disposable test cleanup failed.", cleanupErrors);
            }
        }
    }

    [Phase2ASqlUatFact]
    public async Task DisposableSqlServer_HttpFailureCleanupReuseAndLengthBoundaries()
    {
        var targetConnection = RequireApprovedTarget();
        var target = new SqlConnectionStringBuilder(targetConnection);
        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
        var mediaRoot = Path.Combine(
            Path.GetTempPath(),
            $"MyDocuMgm-phase2b-repair1-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mediaRoot);
        await EnsureDatabaseMissingAsync(master.ConnectionString, databaseName);
        try
        {
            await CreateDatabaseAsync(master.ConnectionString, databaseName);
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            Assert.Equal(new TestCounts(0, 0, 0), await ReadCountsAsync(options));
            await using var factory = new DisposableSqlApiFactory(target.ConnectionString, mediaRoot);
            using var client = factory.CreateClient();

            foreach (var format in new[] { "jpg", "png", "webp" })
            {
                var fixture = CreateImageFixture(format);
                using var createResponse = await client.PostAsJsonAsync(
                    "/api/url-intakes/instagram",
                    new { url = $"https://www.instagram.com/p/Repair1_{format}_{Guid.NewGuid():N}/" });
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                var intake = (await createResponse.Content.ReadFromJsonAsync<UrlIntakeDto>())!;

                var beforeUpload = await ReadCountsAsync(options);
                using var uploadResponse = await UploadAsync(client, intake.Id, fixture);
                Assert.Equal(HttpStatusCode.Created, uploadResponse.Response.StatusCode);
                Assert.False(uploadResponse.Result.Reused);
                var afterUpload = await ReadCountsAsync(options);
                Assert.Equal(beforeUpload.UrlIntakes, afterUpload.UrlIntakes);
                Assert.Equal(beforeUpload.MediaRecords + 1, afterUpload.MediaRecords);

                var stored = await ReadMediaAsync(options, uploadResponse.Result.Item.Id);
                var fullPath = ResolveMediaPath(mediaRoot, stored.RelativePath);
                Assert.True(File.Exists(fullPath));

                using var failedSave = await client.PutAsJsonAsync(
                    $"/api/url-intakes/{intake.Id}/manual-instagram",
                    new
                    {
                        caption = SyntheticFailureCaption,
                        pinnedAuthorCommentState = "NONE",
                        pinnedAuthorCommentText = (string?)null,
                        mediaIds = new[] { uploadResponse.Result.Item.Id }
                    });
                Assert.Equal(HttpStatusCode.BadRequest, failedSave.StatusCode);
                Assert.Equal("SYNTHETIC_FINAL_SAVE_FAILURE", await ReadProblemCodeAsync(failedSave));

                await using (var failedContext = new MyDocuMgmDbContext(options))
                {
                    var preserved = await failedContext.Contents.SingleAsync(value => value.Id == intake.Id);
                    Assert.Null(preserved.ManualCaption);
                    Assert.Null(preserved.PinnedAuthorCommentState);
                    Assert.Null(preserved.PinnedAuthorCommentText);
                    Assert.Equal(IntakeStatus.URL_ACCEPTED, preserved.IntakeStatus);
                    Assert.False(await failedContext.ContentMediaLinks.AnyAsync(
                        link => link.MediaAssetId == uploadResponse.Result.Item.Id));
                }

                var beforeCleanup = await ReadCountsAsync(options);
                using var cleanup = await client.DeleteAsync(
                    $"/api/cleanup/orphan-media/{uploadResponse.Result.Item.Id}");
                Assert.Equal(HttpStatusCode.NoContent, cleanup.StatusCode);
                var afterCleanup = await ReadCountsAsync(options);
                Assert.Equal(beforeCleanup.UrlIntakes, afterCleanup.UrlIntakes);
                Assert.Equal(beforeCleanup.MediaRecords - 1, afterCleanup.MediaRecords);
                Assert.False(File.Exists(fullPath));

                using var cleanupAgain = await client.DeleteAsync(
                    $"/api/cleanup/orphan-media/{uploadResponse.Result.Item.Id}");
                Assert.Equal(HttpStatusCode.NotFound, cleanupAgain.StatusCode);
                Assert.Equal(afterCleanup, await ReadCountsAsync(options));

                _output.WriteLine(
                    "HTTP {0}: create={1}, upload={2}, failed-save={3}, cleanup={4}, cleanup-again={5}; " +
                    "counts before-upload={6}, after-upload={7}, before-cleanup={8}, after-cleanup={9}",
                    format,
                    (int)createResponse.StatusCode,
                    (int)uploadResponse.Response.StatusCode,
                    (int)failedSave.StatusCode,
                    (int)cleanup.StatusCode,
                    (int)cleanupAgain.StatusCode,
                    beforeUpload,
                    afterUpload,
                    beforeCleanup,
                    afterCleanup);
            }

            using var reuseCreate = await client.PostAsJsonAsync(
                "/api/url-intakes/instagram",
                new { url = $"https://www.instagram.com/reel/Repair1_Reuse_{Guid.NewGuid():N}/" });
            Assert.Equal(HttpStatusCode.Created, reuseCreate.StatusCode);
            var reuseIntake = (await reuseCreate.Content.ReadFromJsonAsync<UrlIntakeDto>())!;
            var reuseFixture = CreateImageFixture("png");
            using var originalUpload = await UploadAsync(client, reuseIntake.Id, reuseFixture);
            using var reusedUpload = await UploadAsync(client, reuseIntake.Id, reuseFixture);
            Assert.False(originalUpload.Result.Reused);
            Assert.True(reusedUpload.Result.Reused);
            Assert.Equal(originalUpload.Result.Item.Id, reusedUpload.Result.Item.Id);
            var reusedMedia = await ReadMediaAsync(options, reusedUpload.Result.Item.Id);
            var reusedFullPath = ResolveMediaPath(mediaRoot, reusedMedia.RelativePath);

            using var reusedFailedSave = await client.PutAsJsonAsync(
                $"/api/url-intakes/{reuseIntake.Id}/manual-instagram",
                new
                {
                    caption = SyntheticFailureCaption,
                    pinnedAuthorCommentState = "NONE",
                    pinnedAuthorCommentText = (string?)null,
                    mediaIds = new[] { reusedUpload.Result.Item.Id }
                });
            Assert.Equal(HttpStatusCode.BadRequest, reusedFailedSave.StatusCode);
            Assert.Equal("SYNTHETIC_FINAL_SAVE_FAILURE", await ReadProblemCodeAsync(reusedFailedSave));
            Assert.True(File.Exists(reusedFullPath));
            await using (var reusedContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(1, await reusedContext.MediaAssets.CountAsync(
                    media => media.Id == reusedUpload.Result.Item.Id));
                Assert.False(await reusedContext.ContentMediaLinks.AnyAsync(
                    link => link.MediaAssetId == reusedUpload.Result.Item.Id));
            }
            _output.WriteLine(
                "HTTP reuse: create={0}, original-upload={1}, reused-upload={2}, failed-save={3}; " +
                "cleanup-request-count-before-explicit-teardown=0; media-preserved=True",
                (int)reuseCreate.StatusCode,
                (int)originalUpload.Response.StatusCode,
                (int)reusedUpload.Response.StatusCode,
                (int)reusedFailedSave.StatusCode);

            using var explicitReuseCleanup = await client.DeleteAsync(
                $"/api/cleanup/orphan-media/{reusedUpload.Result.Item.Id}");
            Assert.Equal(HttpStatusCode.NoContent, explicitReuseCleanup.StatusCode);
            Assert.False(File.Exists(reusedFullPath));

            using var boundaryCreate = await client.PostAsJsonAsync(
                "/api/url-intakes/instagram",
                new { url = $"https://www.instagram.com/p/Repair1_Boundary_{Guid.NewGuid():N}/" });
            Assert.Equal(HttpStatusCode.Created, boundaryCreate.StatusCode);
            var boundaryIntake = (await boundaryCreate.Content.ReadFromJsonAsync<UrlIntakeDto>())!;
            var captionBelow = new string('가', Content.ManualCaptionMaxLength - 1);
            var captionExact = string.Concat(
                Enumerable.Repeat("😀", Content.ManualCaptionMaxLength / 2));
            var commentBelow = new string('한', Content.PinnedAuthorCommentMaxLength - 1);
            var commentExact = string.Concat(
                Enumerable.Repeat("😀", Content.PinnedAuthorCommentMaxLength / 2));

            using var captionBelowResponse = await SaveManualAsync(
                client, boundaryIntake.Id, captionBelow, "NONE", null);
            using var captionExactResponse = await SaveManualAsync(
                client, boundaryIntake.Id, captionExact, "NONE", null);
            using var captionTooLongResponse = await SaveManualAsync(
                client, boundaryIntake.Id, captionExact + "가", "NONE", null);
            Assert.Equal(HttpStatusCode.OK, captionBelowResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, captionExactResponse.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, captionTooLongResponse.StatusCode);
            Assert.Equal("MANUAL_CAPTION_TOO_LONG", await ReadProblemCodeAsync(captionTooLongResponse));

            using var commentBelowResponse = await SaveManualAsync(
                client, boundaryIntake.Id, "caption", "PRESENT", commentBelow);
            using var commentExactResponse = await SaveManualAsync(
                client, boundaryIntake.Id, "caption", "PRESENT", commentExact);
            using var commentTooLongResponse = await SaveManualAsync(
                client, boundaryIntake.Id, "caption", "PRESENT", commentExact + "한");
            Assert.Equal(HttpStatusCode.OK, commentBelowResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, commentExactResponse.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, commentTooLongResponse.StatusCode);
            Assert.Equal(
                "PINNED_AUTHOR_COMMENT_TOO_LONG",
                await ReadProblemCodeAsync(commentTooLongResponse));

            await using (var boundaryContext = new MyDocuMgmDbContext(options))
            {
                var preserved = await boundaryContext.Contents.SingleAsync(
                    value => value.Id == boundaryIntake.Id);
                Assert.Equal("caption", preserved.ManualCaption);
                Assert.Equal(commentExact, preserved.PinnedAuthorCommentText);
                var model = boundaryContext.Model.FindEntityType(typeof(Content))!;
                Assert.Equal(
                    Content.ManualCaptionMaxLength,
                    model.FindProperty(nameof(Content.ManualCaption))!.GetMaxLength());
                Assert.Equal(
                    Content.PinnedAuthorCommentMaxLength,
                    model.FindProperty(nameof(Content.PinnedAuthorCommentText))!.GetMaxLength());
                Assert.Equal(-1, await ReadSqlColumnMaxLengthAsync(
                    boundaryContext, nameof(Content.ManualCaption)));
                Assert.Equal(-1, await ReadSqlColumnMaxLengthAsync(
                    boundaryContext, nameof(Content.PinnedAuthorCommentText)));
            }

            _output.WriteLine(
                "HTTP length: caption limit-1={0}, limit={1}, limit+1={2}; " +
                "comment limit-1={3}, limit={4}, limit+1={5}; " +
                "UI/domain/EF limits=20000/10000; SQL CHARACTER_MAXIMUM_LENGTH=-1/-1",
                (int)captionBelowResponse.StatusCode,
                (int)captionExactResponse.StatusCode,
                (int)captionTooLongResponse.StatusCode,
                (int)commentBelowResponse.StatusCode,
                (int)commentExactResponse.StatusCode,
                (int)commentTooLongResponse.StatusCode);

            Assert.Equal(4, factory.FailureInterceptor.FailureCount);
            Assert.True(factory.FailureInterceptor.SawModifiedContent);
            Assert.True(factory.FailureInterceptor.SawAddedMediaLink);
            _output.WriteLine(
                "Failure injection: SaveChanges reached={0}, modified-content={1}, added-media-link={2}",
                factory.FailureInterceptor.FailureCount,
                factory.FailureInterceptor.SawModifiedContent,
                factory.FailureInterceptor.SawAddedMediaLink);

            var finalCounts = await ReadCountsAsync(options);
            Assert.Equal(5, finalCounts.UrlIntakes);
            Assert.Equal(0, finalCounts.MediaRecords);
            Assert.Equal(0, finalCounts.MediaLinks);
            Assert.Empty(Directory.EnumerateFiles(mediaRoot, "*.part", SearchOption.AllDirectories));
            _output.WriteLine("Final disposable state before teardown: {0}", finalCounts);
        }
        finally
        {
            var cleanupErrors = new List<Exception>();
            if (Directory.Exists(mediaRoot))
            {
                try
                {
                    Directory.Delete(mediaRoot, recursive: true);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            SqlConnection.ClearAllPools();
            try
            {
                await DropDatabaseIfExistsAsync(master.ConnectionString, databaseName);
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException(
                    "Phase 2B Repair 1 disposable cleanup failed.",
                    cleanupErrors);
            }
        }
    }

    [Phase2ASqlUatFact]
    public async Task DisposableSqlServer_DeletesOrphanMediaBeforeSoftDeletedOwner()
    {
        var targetConnection = RequireApprovedTarget();
        var target = new SqlConnectionStringBuilder(targetConnection);
        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
        var mediaRoot = Path.Combine(Path.GetTempPath(), $"MyDocuMgm-cleanup-sql-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mediaRoot);
        try
        {
            await EnsureDatabaseMissingAsync(master.ConnectionString, databaseName);
            await CreateDatabaseAsync(master.ConnectionString, databaseName);
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
                Title = "Synthetic cleanup owner"
            };
            content.SoftDelete();
            var mediaId = Guid.NewGuid();
            var storedFileName = $"{mediaId:N}.png";
            var relativePath = $"media/{content.Id:N}/{mediaId:N}/original/{storedFileName}";
            var fullPath = Path.Combine(mediaRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, [1, 2, 3, 4]);
            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                seedContext.Add(content);
                seedContext.Add(new MediaAsset
                {
                    Id = mediaId,
                    ContentId = content.Id,
                    OriginalFileName = "synthetic.png",
                    StoredFileName = storedFileName,
                    RelativePath = relativePath,
                    MimeType = "image/png",
                    SizeBytes = 4,
                    Sha256 = new string('A', 64),
                    Width = 1,
                    Height = 1,
                    StorageStatus = MediaStorageStatus.READY
                });
                await seedContext.SaveChangesAsync();
            }

            var storage = new LocalMediaStorage(Options.Create(new StorageOptions { RootPath = mediaRoot }));
            await using (var mediaContext = new MyDocuMgmDbContext(options))
            {
                var service = new CleanupService(
                    new EfCleanupRepository(mediaContext),
                    storage,
                    new NoopDiagnostics());
                await service.PermanentlyDeleteOrphanMediaAsync(mediaId, default);
            }

            Assert.False(File.Exists(fullPath));
            await using (var contentContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(0, await contentContext.MediaAssets.IgnoreQueryFilters().CountAsync());
                Assert.Equal(1, await contentContext.Contents.IgnoreQueryFilters().CountAsync());
                var service = new CleanupService(
                    new EfCleanupRepository(contentContext),
                    storage,
                    new NoopDiagnostics());
                await service.PermanentlyDeleteContentAsync(content.Id, default);
            }

            await using (var finalContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(0, await finalContext.MediaAssets.IgnoreQueryFilters().CountAsync());
                Assert.Equal(0, await finalContext.Contents.IgnoreQueryFilters().CountAsync());
                Assert.Equal(0, await finalContext.ContentMediaLinks.CountAsync());
            }
        }
        finally
        {
            if (Directory.Exists(mediaRoot)) Directory.Delete(mediaRoot, recursive: true);
            SqlConnection.ClearAllPools();
            await DropDatabaseIfExistsAsync(master.ConnectionString, databaseName);
        }
    }

    private static async Task<UploadEvidence> UploadAsync(
        HttpClient client,
        Guid contentId,
        SyntheticImageFixture fixture)
    {
        using var body = new MultipartFormDataContent();
        using var file = new ByteArrayContent(fixture.Bytes);
        file.Headers.ContentType = new(fixture.MimeType);
        body.Add(file, "file", fixture.FileName);
        var response = await client.PostAsync($"/api/contents/{contentId}/media", body);
        var result = await response.Content.ReadFromJsonAsync<MediaUploadResult>();
        return new UploadEvidence(response, result!);
    }

    private static Task<HttpResponseMessage> SaveManualAsync(
        HttpClient client,
        Guid contentId,
        string caption,
        string state,
        string? comment) =>
        client.PutAsJsonAsync(
            $"/api/url-intakes/{contentId}/manual-instagram",
            new
            {
                caption,
                pinnedAuthorCommentState = state,
                pinnedAuthorCommentText = comment,
                mediaIds = Array.Empty<Guid>()
            });

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<TestProblem>();
        return problem?.Code;
    }

    private static async Task<TestCounts> ReadCountsAsync(
        DbContextOptions<MyDocuMgmDbContext> options)
    {
        await using var context = new MyDocuMgmDbContext(options);
        return new TestCounts(
            await context.Contents.IgnoreQueryFilters().CountAsync(
                content => content.NormalizedUrlHash != null),
            await context.MediaAssets.IgnoreQueryFilters().CountAsync(),
            await context.ContentMediaLinks.CountAsync());
    }

    private static async Task<MediaAsset> ReadMediaAsync(
        DbContextOptions<MyDocuMgmDbContext> options,
        Guid mediaId)
    {
        await using var context = new MyDocuMgmDbContext(options);
        return await context.MediaAssets.IgnoreQueryFilters().AsNoTracking().SingleAsync(
            media => media.Id == mediaId);
    }

    private static string ResolveMediaPath(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static async Task<int> ReadSqlColumnMaxLengthAsync(
        MyDocuMgmDbContext context,
        string columnName)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Contents' AND COLUMN_NAME = @columnName";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@columnName";
            parameter.Value = columnName;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static SyntheticImageFixture CreateImageFixture(string format)
    {
        using var image = new Image<Rgba32>(2, 2, Color.CornflowerBlue);
        using var stream = new MemoryStream();
        var normalized = format.ToLowerInvariant();
        switch (normalized)
        {
            case "jpg":
                image.Save(stream, new JpegEncoder());
                return new SyntheticImageFixture("synthetic.jpg", "image/jpeg", stream.ToArray());
            case "webp":
                image.Save(stream, new WebpEncoder());
                return new SyntheticImageFixture("synthetic.webp", "image/webp", stream.ToArray());
            default:
                image.Save(stream, new PngEncoder());
                return new SyntheticImageFixture("synthetic.png", "image/png", stream.ToArray());
        }
    }

    private sealed record SyntheticImageFixture(string FileName, string MimeType, byte[] Bytes);

    private sealed record TestCounts(int UrlIntakes, int MediaRecords, int MediaLinks);

    private sealed record TestProblem(string Code);

    private sealed record UploadEvidence(HttpResponseMessage Response, MediaUploadResult Result) : IDisposable
    {
        public void Dispose() => Response.Dispose();
    }

    private sealed class DisposableSqlApiFactory(
        string connectionString,
        string mediaRoot) : WebApplicationFactory<Program>
    {
        public DeterministicFinalSaveFailureInterceptor FailureInterceptor { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MyDocuMgm"] = connectionString,
                    ["Storage:RootPath"] = mediaRoot
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<MyDocuMgmDbContext>();
                services.RemoveAll<DbContextOptions<MyDocuMgmDbContext>>();
                services.AddDbContext<MyDocuMgmDbContext>(options =>
                    options.UseSqlServer(connectionString)
                        .AddInterceptors(FailureInterceptor));
                services.Configure<StorageOptions>(options => options.RootPath = mediaRoot);
            });
        }
    }

    private sealed class DeterministicFinalSaveFailureInterceptor : SaveChangesInterceptor
    {
        public int FailureCount { get; private set; }

        public bool SawModifiedContent { get; private set; }

        public bool SawAddedMediaLink { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            var shouldFail = context is not null && context.ChangeTracker.Entries<Content>().Any(entry =>
                entry.State == EntityState.Modified &&
                string.Equals(
                    entry.Entity.ManualCaption,
                    SyntheticFailureCaption,
                    StringComparison.Ordinal));
            if (!shouldFail)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            SawModifiedContent = true;
            SawAddedMediaLink = context!.ChangeTracker.Entries<ContentMediaLink>()
                .Any(entry => entry.State == EntityState.Added);
            FailureCount++;
            throw new DomainRuleException(
                "SYNTHETIC_FINAL_SAVE_FAILURE",
                "Synthetic deterministic final-save persistence failure.");
        }
    }

    private static string RequireApprovedTarget()
    {
        var targetConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(targetConnection))
        {
            throw new InvalidOperationException($"{ConnectionVariable} was not available during SQL UAT execution.");
        }

        var target = new SqlConnectionStringBuilder(targetConnection);
        if (!string.Equals(target.DataSource, @"localhost\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase) ||
            !target.IntegratedSecurity ||
            !string.IsNullOrEmpty(target.UserID) ||
            !string.IsNullOrEmpty(target.Password) ||
            !Regex.IsMatch(target.InitialCatalog, "^MyDocuMgmPhase2A_Validation_[A-Za-z0-9_]+$"))
        {
            throw new InvalidOperationException("Phase 2A disposable SQL Server target is not approved or safe.");
        }

        return targetConnection;
    }

    private sealed class NoopDiagnostics : IMediaDiagnostics
    {
        public void Record(string code, Guid? contentId, Guid? mediaId = null) { }
    }

    private static async Task<UrlIntakeDto> IntakeAsync(
        DbContextOptions<MyDocuMgmDbContext> options,
        string url)
    {
        await using var context = new MyDocuMgmDbContext(options);
        var service = new UrlIntakeService(new EfUrlIntakeRepository(context));
        return await service.IntakeAsync(new CreateUrlIntakeRequest(url), default);
    }

    private static async Task EnsureDatabaseMissingAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT DB_ID(@databaseName)";
        check.Parameters.AddWithValue("@databaseName", databaseName);
        if (await check.ExecuteScalarAsync() is not DBNull)
        {
            throw new InvalidOperationException("Phase 2A disposable database already exists.");
        }
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}
