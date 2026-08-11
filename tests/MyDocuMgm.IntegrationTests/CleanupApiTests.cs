using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class CleanupApiTests
{
    [Fact]
    public async Task CleanupEndpoints_ExposeTrashAndEnforceContentDeleteOrder()
    {
        var content = new Content { IsDeleted = true, Title = "synthetic trash", RowVersion = [1] };
        var repository = new FakeCleanupRepository { Content = content, OwnedMediaCount = 1 };
        await using var factory = new CleanupFactory(repository);
        using var client = factory.CreateClient();

        var trash = await client.GetFromJsonAsync<List<TrashContentItem>>("/api/cleanup/trash");
        Assert.Single(trash!);

        using var blocked = await client.DeleteAsync($"/api/cleanup/trash/{content.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        Assert.Equal("CONTENT_HAS_OWNED_MEDIA", problem.RootElement.GetProperty("code").GetString());
        Assert.False(repository.ContentRemoved);
    }

    [Fact]
    public async Task CleanupEndpoints_DeleteOneOrphanAndThenOneSoftDeletedContent()
    {
        var content = new Content { IsDeleted = true, Title = "synthetic trash", RowVersion = [1] };
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(), ContentId = content.Id, OriginalFileName = "synthetic.png",
            StoredFileName = "file.png", RelativePath = "media/content/media/original/file.png", RowVersion = [1]
        };
        var repository = new FakeCleanupRepository
        {
            Content = content,
            Candidate = new CleanupMediaCandidate(media, 0, 0)
        };
        await using var factory = new CleanupFactory(repository);
        using var client = factory.CreateClient();

        using var mediaResult = await client.DeleteAsync($"/api/cleanup/orphan-media/{media.Id}");
        Assert.Equal(HttpStatusCode.NoContent, mediaResult.StatusCode);
        Assert.True(repository.MediaRemoved);
        Assert.Equal(1, factory.Storage.CommitCount);

        repository.OwnedMediaCount = 0;
        using var contentResult = await client.DeleteAsync($"/api/cleanup/trash/{content.Id}");
        Assert.Equal(HttpStatusCode.NoContent, contentResult.StatusCode);
        Assert.True(repository.ContentRemoved);
    }

    private sealed class CleanupFactory(FakeCleanupRepository repository) : WebApplicationFactory<Program>
    {
        public FakeCleanupStorage Storage { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.ClearProviders());
                services.RemoveAll<ICleanupRepository>();
                services.RemoveAll<IMediaCleanupStorage>();
                services.RemoveAll<IMediaDiagnostics>();
                services.AddSingleton<ICleanupRepository>(repository);
                services.AddSingleton<IMediaCleanupStorage>(Storage);
                services.AddSingleton<IMediaDiagnostics, NoopDiagnostics>();
            });
        }
    }

    private sealed class FakeCleanupRepository : ICleanupRepository
    {
        public Content? Content { get; init; }
        public CleanupMediaCandidate? Candidate { get; init; }
        public int OwnedMediaCount { get; set; }
        public bool ContentRemoved { get; private set; }
        public bool MediaRemoved { get; private set; }
        public Task<IReadOnlyList<TrashContentItem>> ListTrashAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrashContentItem>>(Content is null ? [] :
                [new TrashContentItem(Content.Id, Content.Title, DateTime.UtcNow, OwnedMediaCount, Convert.ToBase64String(Content.RowVersion))]);
        public Task<Content?> FindContentAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Content);
        public Task<int> CountOwnedMediaAsync(Guid contentId, CancellationToken cancellationToken) => Task.FromResult(OwnedMediaCount);
        public Task RemoveSourceEvidenceAsync(Guid contentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public void RemoveContent(Content content) => ContentRemoved = true;
        public Task<IReadOnlyList<CleanupMediaCandidate>> ListOrphanMediaAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CleanupMediaCandidate>>(Candidate is null ? [] : [Candidate]);
        public Task<CleanupMediaCandidate?> FindMediaAsync(Guid mediaId, CancellationToken cancellationToken) => Task.FromResult(Candidate);
        public Task<int> CountMediaPathReferencesAsync(string relativePath, Guid excludingMediaId, CancellationToken cancellationToken) => Task.FromResult(0);
        public void RemoveMedia(MediaAsset media) => MediaRemoved = true;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCleanupStorage : IMediaCleanupStorage
    {
        public int CommitCount { get; private set; }
        public Task<MediaCleanupStorageState> InspectAsync(Guid contentId, Guid mediaId, string relativePath, string storedFileName, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaCleanupStorageState(true, true, "MEDIA_FILE_READY_FOR_CLEANUP"));
        public Task<PreparedMediaCleanup> PrepareDeleteAsync(Guid contentId, Guid mediaId, string relativePath, string storedFileName, CancellationToken cancellationToken) =>
            Task.FromResult(new PreparedMediaCleanup([], false));
        public Task RestoreAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CommitAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopDiagnostics : IMediaDiagnostics
    {
        public void Record(string code, Guid? contentId, Guid? mediaId = null) { }
    }
}
