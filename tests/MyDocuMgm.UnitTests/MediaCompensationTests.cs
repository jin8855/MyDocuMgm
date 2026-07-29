using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class MediaCompensationTests
{
    [Fact]
    public async Task ReadyDatabaseFailure_DeletesStoredFileAndMarksFailed()
    {
        var content = new Content();
        var contentRepository = new FakeContentRepository(content);
        var mediaRepository = new FailingMediaRepository();
        var storage = new RecordingStorage();
        var service = new MediaService(contentRepository, mediaRepository, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadAsync(content.Id, new MemoryStream([1]), "a.png", "image/png", default));

        Assert.Equal("safe/path.png", storage.DeletedPath);
        Assert.Equal(MediaStorageStatus.FAILED, mediaRepository.Media!.StorageStatus);
        Assert.Equal(3, mediaRepository.SaveCount);
    }

    private sealed class FakeContentRepository(Content content) : IContentRepository
    {
        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(content);
        public Task AddAsync(Content value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailingMediaRepository : IMediaAssetRepository
    {
        public MediaAsset? Media { get; private set; }
        public int SaveCount { get; private set; }
        public Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult(Media);
        public Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Media is null ? [] : [Media]);
        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) { Media = media; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (SaveCount == 2) throw new InvalidOperationException("simulated database failure");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStorage : IMediaStorage
    {
        public string? DeletedPath { get; private set; }
        public Task<StoredMedia> StoreAsync(Stream source, string originalFileName, string declaredMimeType, CancellationToken cancellationToken) =>
            Task.FromResult(new StoredMedia("path.png", "safe/path.png", "image/png", 1, new string('A', 64), 1, 1));
        public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            DeletedPath = relativePath;
            return Task.CompletedTask;
        }
    }
}
