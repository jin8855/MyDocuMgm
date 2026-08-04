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
        var mediaRepository = new FailingMediaRepository(failAtSave: 2);
        var storage = new RecordingStorage();
        var service = new MediaService(contentRepository, mediaRepository, storage);

        await Assert.ThrowsAsync<MediaOperationException>(
            () => service.UploadAsync(content.Id, new MemoryStream([1]), "a.png", "image/png", default));

        Assert.Equal("safe/path.png", storage.DeletedPath);
        Assert.Equal(MediaStorageStatus.FAILED, mediaRepository.Media!.StorageStatus);
        Assert.Equal(3, mediaRepository.SaveCount);
    }

    [Fact]
    public async Task PendingDatabaseFailure_DiscardsTemporaryFile()
    {
        var content = new Content();
        var mediaRepository = new FailingMediaRepository(failAtSave: 1);
        var storage = new RecordingStorage();
        var service = new MediaService(new FakeContentRepository(content), mediaRepository, storage);

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => service.UploadAsync(
                content.Id,
                new MemoryStream([1]),
                "pending.png",
                "image/png",
                default));

        Assert.Equal("MEDIA_PERSISTENCE_FAILED", error.Code);
        Assert.True(storage.Discarded);
        Assert.False(storage.Promoted);
    }

    [Fact]
    public async Task PromotionFailure_MarksAssetFailedWithStableCode()
    {
        var content = new Content();
        var mediaRepository = new FailingMediaRepository();
        var storage = new RecordingStorage { FailPromotion = true };
        var service = new MediaService(new FakeContentRepository(content), mediaRepository, storage);

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => service.UploadAsync(
                content.Id,
                new MemoryStream([1]),
                "promote.png",
                "image/png",
                default));

        Assert.Equal("MEDIA_PROMOTION_FAILED", error.Code);
        Assert.Equal(MediaStorageStatus.FAILED, mediaRepository.Media!.StorageStatus);
        Assert.Equal("MEDIA_PROMOTION_FAILED", mediaRepository.Media.FailureReason);
        Assert.Equal(2, mediaRepository.SaveCount);
    }

    private sealed class FakeContentRepository(Content content) : IContentRepository
    {
        public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<Content?>(content);
        public Task AddAsync(Content value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailingMediaRepository(int? failAtSave = null) : IMediaAssetRepository
    {
        public MediaAsset? Media { get; private set; }
        public int SaveCount { get; private set; }
        public Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult(Media);
        public Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Media is null ? [] : [Media]);
        public Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaPage([], 0, 0, 0, 0, query.Page, query.PageSize));
        public Task<MediaAsset?> FindReadyDuplicateAsync(Guid contentId, string sha256, long sizeBytes, CancellationToken cancellationToken) =>
            Task.FromResult<MediaAsset?>(null);
        public Task<IReadOnlyList<MediaAsset>> ListAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Media is null ? [] : [Media]);
        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) { Media = media; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (SaveCount == failAtSave) throw new InvalidOperationException("simulated database failure");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStorage : IMediaStorage
    {
        public string? DeletedPath { get; private set; }
        public bool Discarded { get; private set; }
        public bool Promoted { get; private set; }
        public bool FailPromotion { get; init; }
        public Task<PreparedMedia> PrepareAsync(Stream source, Guid contentId, Guid mediaId, string originalFileName, string declaredMimeType, CancellationToken cancellationToken) =>
            Task.FromResult(new PreparedMedia("temp/file.part", "path.png", "safe/path.png", "image/png", 1, new string('A', 64), 1, 1));
        public Task PromoteAsync(PreparedMedia prepared, CancellationToken cancellationToken)
        {
            if (FailPromotion)
            {
                throw new MediaOperationException("MEDIA_PROMOTION_FAILED", "simulated promotion failure");
            }

            Promoted = true;
            return Task.CompletedTask;
        }
        public Task DiscardPreparedAsync(PreparedMedia prepared, CancellationToken cancellationToken)
        {
            Discarded = true;
            return Task.CompletedTask;
        }
        public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            DeletedPath = relativePath;
            return Task.CompletedTask;
        }
        public Task<MediaBinary> OpenOriginalAsync(string relativePath, string mimeType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<MediaBinary> GetOrCreateThumbnailAsync(Guid mediaId, string relativePath, string expectedMimeType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<MediaIntegrityResult> VerifyAsync(string relativePath, string expectedMimeType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaIntegrityResult(true, "MEDIA_INTEGRITY_OK"));
        public Task<MediaReconciliationReport> ReconcileAsync(IReadOnlyList<MediaStorageReference> references, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaReconciliationReport(0, 0, 0, 0, 0, 0, []));
    }
}
