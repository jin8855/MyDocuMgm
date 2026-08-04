using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.Storage;

namespace MyDocuMgm.IntegrationTests;

public sealed class MediaApiEndToEndTests : IDisposable
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"MyDocuMgm-api-media-{Guid.NewGuid():N}");
    private readonly Guid _contentId = Guid.NewGuid();
    private readonly Guid _secondContentId = Guid.NewGuid();
    private readonly InMemoryContentRepository _contents;
    private readonly InMemoryMediaRepository _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public MediaApiEndToEndTests()
    {
        Directory.CreateDirectory(_root);
        _contents = new(
            new Content { Id = _contentId },
            new Content { Id = _secondContentId });
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MyDocuMgm"] = "configured-but-never-opened",
                    ["Storage:RootPath"] = _root
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContentRepository>();
                services.RemoveAll<IMediaAssetRepository>();
                services.RemoveAll<IMediaStorage>();
                services.RemoveAll<IMediaDiagnostics>();
                services.AddSingleton<IContentRepository>(_contents);
                services.AddSingleton<IMediaAssetRepository>(_media);
                services.AddSingleton<IMediaStorage>(
                    new LocalMediaStorage(Options.Create(new StorageOptions { RootPath = _root })));
                services.AddSingleton<IMediaDiagnostics, NoopDiagnostics>();
            });
        });
    }

    [Fact]
    public async Task UploadOriginalThumbnailSoftDeleteAndRestore_WorkWithoutDatabase()
    {
        using var client = _factory.CreateClient();

        var first = await UploadAsync(client, "tiny.png", PngBytes);
        Assert.Equal(HttpStatusCode.Created, first.Response.StatusCode);
        Assert.False(first.Result.Reused);
        Assert.Equal(MediaStorageStatus.READY, first.Result.Item.StorageStatus);
        Assert.Single(_media.Items);
        Assert.Equal(
            [MediaStorageStatus.PENDING, MediaStorageStatus.READY],
            _media.SaveStates.Take(2));
        Assert.Matches(
            $"^media/{_contentId:N}/{first.Result.Item.Id:N}/original/[0-9a-f]{{32}}\\.png$",
            _media.Items[0].RelativePath);

        var duplicate = await UploadAsync(client, "tiny.png", PngBytes);
        Assert.True(duplicate.Result.Reused);
        Assert.Single(_media.Items);

        var original = await client.GetByteArrayAsync(
            $"/api/contents/{_contentId}/media/{first.Result.Item.Id}/file");
        Assert.Equal(PngBytes, original);

        using var thumbnailResponse = await client.GetAsync(
            $"/api/contents/{_contentId}/media/{first.Result.Item.Id}/thumbnail");
        Assert.Equal(HttpStatusCode.OK, thumbnailResponse.StatusCode);
        Assert.Equal("image/webp", thumbnailResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await thumbnailResponse.Content.ReadAsByteArrayAsync());

        using var deleteResponse = await client.DeleteAsync(
            $"/api/contents/{_contentId}/media/{first.Result.Item.Id}?rowVersion={Uri.EscapeDataString(first.Result.Item.RowVersion)}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(_media.Items[0].IsDeleted);
        Assert.True(File.Exists(FullPath(_media.Items[0].RelativePath)));
        using var deletedFileResponse = await client.GetAsync(
            $"/api/contents/{_contentId}/media/{first.Result.Item.Id}/file");
        Assert.Equal(HttpStatusCode.NotFound, deletedFileResponse.StatusCode);

        var normal = await client.GetFromJsonAsync<MediaPage>(
            $"/api/contents/{_contentId}/media?filter=ALL&page=1&pageSize=24");
        var deleted = await client.GetFromJsonAsync<MediaPage>(
            $"/api/contents/{_contentId}/media?filter=DELETED&page=1&pageSize=24");
        Assert.Empty(normal!.Items);
        Assert.Single(deleted!.Items);

        using var restoreResponse = await client.PostAsJsonAsync(
            $"/api/contents/{_contentId}/media/{first.Result.Item.Id}/restore",
            new { rowVersion = first.Result.Item.RowVersion });
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        Assert.False(_media.Items[0].IsDeleted);
    }

    [Fact]
    public async Task SameBytesAcrossContents_CreateSeparateManagedCopies()
    {
        using var client = _factory.CreateClient();
        var first = await UploadAsync(client, "first.png", PngBytes);
        using var body = Multipart("second.png", PngBytes, "image/png");
        using var response = await client.PostAsync(
            $"/api/contents/{_secondContentId}/media",
            body);
        var second = await response.Content.ReadFromJsonAsync<MediaUploadResult>();

        Assert.False(first.Result.Reused);
        Assert.False(second!.Reused);
        Assert.Equal(2, _media.Items.Count);
        Assert.NotEqual(_media.Items[0].RelativePath, _media.Items[1].RelativePath);
    }

    [Fact]
    public async Task ConcurrentSameContentUpload_ReusesOneReadyAssetWithinProcess()
    {
        using var client = _factory.CreateClient();

        var results = await Task.WhenAll(
            UploadAsync(client, "parallel.png", PngBytes),
            UploadAsync(client, "parallel.png", PngBytes));

        Assert.Single(_media.Items);
        Assert.Single(results, result => result.Result.Reused);
        Assert.Single(results, result => !result.Result.Reused);
    }

    [Fact]
    public async Task RestoreFailsClosedWhenOriginalIntegrityChanged()
    {
        using var client = _factory.CreateClient();
        var uploaded = await UploadAsync(client, "integrity.png", PngBytes);
        using var deleteResponse = await client.DeleteAsync(
            $"/api/contents/{_contentId}/media/{uploaded.Result.Item.Id}?rowVersion={Uri.EscapeDataString(uploaded.Result.Item.RowVersion)}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await File.AppendAllBytesAsync(FullPath(_media.Items[0].RelativePath), [0]);
        using var restoreResponse = await client.PostAsJsonAsync(
            $"/api/contents/{_contentId}/media/{uploaded.Result.Item.Id}/restore",
            new { rowVersion = uploaded.Result.Item.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, restoreResponse.StatusCode);
        Assert.Equal(
            "MEDIA_RESTORE_BLOCKED",
            (await restoreResponse.Content.ReadFromJsonAsync<Problem>())!.Code);
        Assert.True(_media.Items[0].IsDeleted);
    }

    [Fact]
    public async Task MissingContentAndUnsupportedExtension_ReturnStableCodes()
    {
        using var client = _factory.CreateClient();
        using var missingBody = Multipart("tiny.png", PngBytes, "image/png");
        using var missing = await client.PostAsync(
            $"/api/contents/{Guid.NewGuid()}/media",
            missingBody);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            "MEDIA_CONTENT_NOT_FOUND",
            (await missing.Content.ReadFromJsonAsync<Problem>())!.Code);

        using var unsupportedBody = Multipart("tiny.gif", [1, 2, 3], "image/gif");
        using var unsupported = await client.PostAsync(
            $"/api/contents/{_contentId}/media",
            unsupportedBody);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        Assert.Equal(
            "MEDIA_EXTENSION_NOT_ALLOWED",
            (await unsupported.Content.ReadFromJsonAsync<Problem>())!.Code);
        var safeProblem = await unsupported.Content.ReadAsStringAsync();
        Assert.DoesNotContain(_root, safeProblem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tiny.gif", safeProblem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadyHealthChecksExplicitRootWriteabilityWithoutDatabaseProbe()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("MEDIA_STORAGE_READY", body);
        Assert.Contains("configured-not-probed", body);
        Assert.Empty(Directory.EnumerateFiles(_root, ".readiness-*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(HttpResponseMessage Response, MediaUploadResult Result)> UploadAsync(
        HttpClient client,
        string fileName,
        byte[] bytes)
    {
        using var body = Multipart(fileName, bytes, "image/png");
        var response = await client.PostAsync($"/api/contents/{_contentId}/media", body);
        var result = await response.Content.ReadFromJsonAsync<MediaUploadResult>();
        return (response, result!);
    }

    private static MultipartFormDataContent Multipart(
        string fileName,
        byte[] bytes,
        string mimeType)
    {
        var body = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new(mimeType);
        body.Add(file, "file", fileName);
        return body;
    }

    private string FullPath(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private sealed record Problem(string Code);

    private sealed class NoopDiagnostics : IMediaDiagnostics
    {
        public void Record(string code, Guid? contentId, Guid? mediaId = null)
        {
        }
    }

    private sealed class InMemoryContentRepository(params Content[] contents) : IContentRepository
    {
        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(contents.SingleOrDefault(content => content.Id == id));
        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task AddAsync(Content value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemoryMediaRepository : IMediaAssetRepository
    {
        public List<MediaAsset> Items { get; } = [];
        public List<MediaStorageStatus> SaveStates { get; } = [];

        public Task<MediaAsset?> FindAsync(
            Guid contentId,
            Guid mediaId,
            bool includeDeleted,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item =>
                item.ContentId == contentId &&
                item.Id == mediaId &&
                (includeDeleted || !item.IsDeleted)));

        public Task<IReadOnlyList<MediaAsset>> ListAsync(
            Guid contentId,
            bool includeDeleted,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Items
                .Where(item =>
                    item.ContentId == contentId &&
                    item.StorageStatus == MediaStorageStatus.READY &&
                    (includeDeleted || !item.IsDeleted))
                .OrderBy(item => item.SortOrder)
                .ToList());

        public Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken)
        {
            var ready = Items.Where(item =>
                item.ContentId == query.ContentId &&
                item.StorageStatus == MediaStorageStatus.READY &&
                !item.IsDeleted).ToList();
            var deleted = Items.Where(item =>
                item.ContentId == query.ContentId &&
                item.StorageStatus == MediaStorageStatus.READY &&
                item.IsDeleted).ToList();
            var duplicateHashes = ready.GroupBy(item => item.Sha256)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet();
            var filtered = query.Filter switch
            {
                MediaFilter.SELECTED => ready.Where(item => item.IsSelected).ToList(),
                MediaFilter.DUPLICATE => ready.Where(item => duplicateHashes.Contains(item.Sha256)).ToList(),
                MediaFilter.DELETED => deleted,
                _ => ready
            };
            var items = filtered.Select(ToDto).ToList();
            return Task.FromResult(new MediaPage(
                items,
                items.Count,
                ready.Count(item => item.IsSelected),
                ready.Count(item => duplicateHashes.Contains(item.Sha256)),
                deleted.Count,
                query.Page,
                query.PageSize));
        }

        public Task<MediaAsset?> FindReadyDuplicateAsync(
            Guid contentId,
            string sha256,
            long sizeBytes,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(item =>
                item.ContentId == contentId &&
                !item.IsDeleted &&
                item.StorageStatus == MediaStorageStatus.READY &&
                item.Sha256 == sha256 &&
                item.SizeBytes == sizeBytes));

        public Task<IReadOnlyList<MediaAsset>> ListAllAsync(
            bool includeDeleted,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Items
                .Where(item => includeDeleted || !item.IsDeleted)
                .ToList());

        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken)
        {
            media.RowVersion = [1];
            Items.Add(media);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (Items.Count > 0)
            {
                SaveStates.Add(Items[^1].StorageStatus);
            }

            return Task.CompletedTask;
        }

        private static MediaItemDto ToDto(MediaAsset media) =>
            new(
                media.Id,
                media.OriginalFileName,
                $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.SizeBytes,
                media.Width,
                media.Height,
                media.SortOrder,
                media.SourceTimestampMs,
                media.IsSelected,
                media.IsPublicAllowed,
                media.Description,
                media.StorageStatus,
                media.Sha256,
                media.IsDeleted,
                media.DeletedAtUtc,
                Convert.ToBase64String(media.RowVersion));
    }
}
