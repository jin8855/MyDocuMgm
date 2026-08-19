using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class CleanupServiceTests
{
    [Fact]
    public async Task PermanentContentDelete_RejectsActiveAndOwnedMedia()
    {
        var active = new Content { IsDeleted = false };
        var activeRepository = new FakeCleanupRepository { Content = active };
        var activeService = CreateService(activeRepository);
        var activeError = await Assert.ThrowsAsync<CleanupConflictException>(
            () => activeService.PermanentlyDeleteContentAsync(active.Id, default));
        Assert.Equal("CONTENT_NOT_SOFT_DELETED", activeError.Code);

        var deleted = new Content { IsDeleted = true };
        var ownedRepository = new FakeCleanupRepository { Content = deleted, OwnedMediaCount = 1 };
        var ownedService = CreateService(ownedRepository);
        var ownedError = await Assert.ThrowsAsync<CleanupConflictException>(
            () => ownedService.PermanentlyDeleteContentAsync(deleted.Id, default));
        Assert.Equal("CONTENT_HAS_OWNED_MEDIA", ownedError.Code);
        Assert.False(ownedRepository.ContentRemoved);
    }

    [Fact]
    public async Task PermanentContentDelete_RemovesOnlySoftDeletedContentWithoutOwnedMedia()
    {
        var content = new Content { IsDeleted = true };
        var repository = new FakeCleanupRepository { Content = content };

        await CreateService(repository).PermanentlyDeleteContentAsync(content.Id, default);

        Assert.True(repository.ContentRemoved);
        Assert.Equal(["evidence", "content", "save"], repository.RemovalOrder);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task PermanentMediaDelete_RejectsReferencesAndSharedPathBeforeFileChange()
    {
        var media = Media();
        var linked = new FakeCleanupRepository
        {
            Candidate = new CleanupMediaCandidate(media, 1, 0)
        };
        var linkedStorage = new FakeCleanupStorage();
        var linkedError = await Assert.ThrowsAsync<CleanupConflictException>(
            () => CreateService(linked, linkedStorage).PermanentlyDeleteOrphanMediaAsync(media.Id, default));
        Assert.Equal("MEDIA_STILL_REFERENCED", linkedError.Code);
        Assert.Equal(0, linkedStorage.PrepareCount);

        var shared = new FakeCleanupRepository
        {
            Candidate = new CleanupMediaCandidate(media, 0, 0),
            SharedPathCount = 1
        };
        var sharedStorage = new FakeCleanupStorage();
        var sharedError = await Assert.ThrowsAsync<CleanupConflictException>(
            () => CreateService(shared, sharedStorage).PermanentlyDeleteOrphanMediaAsync(media.Id, default));
        Assert.Equal("MEDIA_PATH_SHARED", sharedError.Code);
        Assert.Equal(0, sharedStorage.PrepareCount);
    }

    [Fact]
    public async Task PermanentMediaDelete_RestoresQuarantineWhenDatabaseDeleteFails()
    {
        var media = Media();
        var repository = new FakeCleanupRepository
        {
            Candidate = new CleanupMediaCandidate(media, 0, 0),
            FailSave = true
        };
        var storage = new FakeCleanupStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(repository, storage).PermanentlyDeleteOrphanMediaAsync(media.Id, default));

        Assert.Equal(1, storage.PrepareCount);
        Assert.Equal(1, storage.RestoreCount);
        Assert.Equal(0, storage.CommitCount);
    }

    [Fact]
    public async Task PermanentMediaDelete_CommitsQuarantineAfterDatabaseDelete()
    {
        var media = Media();
        var repository = new FakeCleanupRepository
        {
            Candidate = new CleanupMediaCandidate(media, 0, 0)
        };
        var storage = new FakeCleanupStorage();

        await CreateService(repository, storage).PermanentlyDeleteOrphanMediaAsync(media.Id, default);

        Assert.True(repository.MediaRemoved);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(1, storage.PrepareCount);
        Assert.Equal(0, storage.RestoreCount);
        Assert.Equal(1, storage.CommitCount);
    }

    private static CleanupService CreateService(
        FakeCleanupRepository repository,
        FakeCleanupStorage? storage = null) =>
        new(repository, storage ?? new FakeCleanupStorage(), new NoopDiagnostics());

    private static MediaAsset Media() => new()
    {
        Id = Guid.NewGuid(),
        ContentId = Guid.NewGuid(),
        RelativePath = "media/content/media/original/file.png",
        StoredFileName = "file.png",
        OriginalFileName = "synthetic.png",
        RowVersion = [1]
    };

    private sealed class FakeCleanupRepository : ICleanupRepository
    {
        public Content? Content { get; init; }
        public CleanupMediaCandidate? Candidate { get; init; }
        public int OwnedMediaCount { get; init; }
        public int SharedPathCount { get; init; }
        public bool FailSave { get; init; }
        public bool ContentRemoved { get; private set; }
        public bool MediaRemoved { get; private set; }
        public int SaveCount { get; private set; }
        public List<string> RemovalOrder { get; } = [];
        public Task<IReadOnlyList<TrashContentItem>> ListTrashAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrashContentItem>>([]);
        public Task<Content?> FindContentAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Content);
        public Task<int> CountOwnedMediaAsync(Guid contentId, CancellationToken cancellationToken) => Task.FromResult(OwnedMediaCount);
        public Task RemoveSourceEvidenceAsync(Guid contentId, CancellationToken cancellationToken)
        {
            RemovalOrder.Add("evidence");
            return Task.CompletedTask;
        }
        public void RemoveContent(Content content)
        {
            RemovalOrder.Add("content");
            ContentRemoved = true;
        }
        public Task<IReadOnlyList<CleanupMediaCandidate>> ListOrphanMediaAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CleanupMediaCandidate>>(Candidate is null ? [] : [Candidate]);
        public Task<CleanupMediaCandidate?> FindMediaAsync(Guid mediaId, CancellationToken cancellationToken) => Task.FromResult(Candidate);
        public Task<int> CountMediaPathReferencesAsync(string relativePath, Guid excludingMediaId, CancellationToken cancellationToken) =>
            Task.FromResult(SharedPathCount);
        public void RemoveMedia(MediaAsset media) => MediaRemoved = true;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            RemovalOrder.Add("save");
            SaveCount++;
            if (FailSave) throw new InvalidOperationException("simulated database failure");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCleanupStorage : IMediaCleanupStorage
    {
        public int PrepareCount { get; private set; }
        public int RestoreCount { get; private set; }
        public int CommitCount { get; private set; }
        public Task<MediaCleanupStorageState> InspectAsync(Guid contentId, Guid mediaId, string relativePath, string storedFileName, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaCleanupStorageState(true, true, "MEDIA_FILE_READY_FOR_CLEANUP"));
        public Task<PreparedMediaCleanup> PrepareDeleteAsync(Guid contentId, Guid mediaId, string relativePath, string storedFileName, CancellationToken cancellationToken)
        {
            PrepareCount++;
            return Task.FromResult(new PreparedMediaCleanup(
                [new QuarantinedMediaFile(relativePath, "temp/cleanup/file.png")],
                false));
        }
        public Task RestoreAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken)
        {
            RestoreCount++;
            return Task.CompletedTask;
        }
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
