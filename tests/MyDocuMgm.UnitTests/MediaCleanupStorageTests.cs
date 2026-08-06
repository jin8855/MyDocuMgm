using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using MyDocuMgm.Infrastructure.Storage;
using Xunit.Sdk;

namespace MyDocuMgm.UnitTests;

public sealed class MediaCleanupStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"MyDocuMgmCleanup_{Guid.NewGuid():N}");

    public MediaCleanupStorageTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task PrepareAndRestore_MoveOriginalAndDerivedFilesWithoutDeletingThem()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        var original = Full(relative);
        var thumbnail = Full($"derived/thumbnails/{mediaId:N}/thumb.webp");
        Write(original, [1, 2, 3]);
        Write(thumbnail, [4, 5, 6]);
        var storage = CreateStorage();

        var cleanup = await storage.PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default);
        Assert.False(File.Exists(original));
        Assert.False(File.Exists(thumbnail));
        Assert.Equal(2, cleanup.Files.Count);

        await storage.RestoreAsync(cleanup, default);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(original));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(thumbnail));
    }

    [Fact]
    public async Task PrepareAndCommit_RemoveOriginalAndDerivedFiles()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        var original = Full(relative);
        var thumbnail = Full($"derived/thumbnails/{mediaId:N}/thumb.webp");
        Write(original, [1]);
        Write(thumbnail, [2]);
        var storage = CreateStorage();

        var cleanup = await storage.PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default);
        await storage.CommitAsync(cleanup, default);

        Assert.False(File.Exists(original));
        Assert.False(File.Exists(thumbnail));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories),
            file => file.Contains("cleanup", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("C:/outside/file.png")]
    [InlineData("//server/share/file.png")]
    [InlineData("Z:/outside/file.png")]
    [InlineData("media-similar/content/media/original/file.png")]
    [InlineData("temp/victim.png")]
    public async Task PrepareDelete_RejectsPathsOutsideAuthoritativeOwnershipLayout(string relativePath)
    {
        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => CreateStorage().PrepareDeleteAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                relativePath,
                "file.png",
                default));

        Assert.Equal("MEDIA_CLEANUP_IDENTITY_INVALID", error.Code);
    }

    [Fact]
    public async Task PrepareDelete_RejectsStoredFileNameThatDoesNotMatchAuthoritativePath()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => CreateStorage().PrepareDeleteAsync(
                contentId,
                mediaId,
                Relative(contentId, mediaId),
                "other.png",
                default));

        Assert.Equal("MEDIA_CLEANUP_IDENTITY_INVALID", error.Code);
    }

    [Fact]
    public async Task PrepareDelete_RejectsDirectoryAtAuthoritativeFilePath()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        Directory.CreateDirectory(Full(relative));

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => CreateStorage().PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default));

        Assert.Equal("MEDIA_CLEANUP_TARGET_INVALID", error.Code);
    }

    [Fact]
    public async Task PrepareDelete_RejectsReparsePointInAuthoritativePath()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var externalRoot = Path.Combine(Path.GetTempPath(), $"MyDocuMgmCleanupOutside_{Guid.NewGuid():N}");
        var link = Full($"media/{contentId:N}");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        Directory.CreateDirectory(externalRoot);
        Write(Path.Combine(externalRoot, $"{mediaId:N}", "original", "file.png"), [1]);

        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, externalRoot);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                throw SkipException.ForSkip(
                    $"$XunitDynamicSkip$Reparse-point creation is unavailable: {exception.GetType().Name}");
            }

            var error = await Assert.ThrowsAsync<MediaOperationException>(
                () => CreateStorage().PrepareDeleteAsync(
                    contentId,
                    mediaId,
                    Relative(contentId, mediaId),
                    "file.png",
                    default));

            Assert.Equal("MEDIA_STORAGE_PATH_INVALID", error.Code);
            Assert.True(File.Exists(Path.Combine(externalRoot, $"{mediaId:N}", "original", "file.png")));
        }
        finally
        {
            if (Directory.Exists(externalRoot))
            {
                Directory.Delete(externalRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareDelete_SecondMoveFailure_RestoresFirstFile()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        var original = Full(relative);
        var thumbnail = Full($"derived/thumbnails/{mediaId:N}/thumb.webp");
        Write(original, [1]);
        Write(thumbnail, [2]);
        var injector = new FaultInjector { FailPrepareMoveAt = 1 };

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => CreateStorage(injector).PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default));

        Assert.Equal("MEDIA_CLEANUP_PREPARE_FAILED", error.Code);
        Assert.True(File.Exists(original));
        Assert.True(File.Exists(thumbnail));
        Assert.Empty(CleanupQuarantineFiles());
    }

    [Fact]
    public async Task PrepareDelete_RestoreFailure_ReportsDistinctResidualState()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        var original = Full(relative);
        var thumbnail = Full($"derived/thumbnails/{mediaId:N}/thumb.webp");
        Write(original, [1]);
        Write(thumbnail, [2]);
        var injector = new FaultInjector { FailPrepareMoveAt = 1, FailRestoreMoveAt = 0 };

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => CreateStorage(injector).PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default));

        Assert.Equal("MEDIA_CLEANUP_PREPARE_RESTORE_FAILED", error.Code);
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(thumbnail));
        Assert.Single(CleanupQuarantineFiles());
    }

    [Fact]
    public async Task Commit_SecondDeleteFailure_DoesNotReportSuccessAndPreservesResidualQuarantine()
    {
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var relative = Relative(contentId, mediaId);
        Write(Full(relative), [1]);
        Write(Full($"derived/thumbnails/{mediaId:N}/thumb.webp"), [2]);
        var injector = new FaultInjector();
        var storage = CreateStorage(injector);
        var cleanup = await storage.PrepareDeleteAsync(contentId, mediaId, relative, "file.png", default);
        injector.FailDeleteAt = 1;

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => storage.CommitAsync(cleanup, default));

        Assert.Equal("MEDIA_CLEANUP_FINALIZE_FAILED", error.Code);
        Assert.Single(CleanupQuarantineFiles());
    }

    private LocalMediaStorage CreateStorage(IMediaCleanupFaultInjector? injector = null) =>
        new(Options.Create(new StorageOptions { RootPath = _root }))
        {
            CleanupFaultInjector = injector ?? NoopMediaCleanupFaultInjector.Instance
        };

    private static string Relative(Guid contentId, Guid mediaId) =>
        $"media/{contentId:N}/{mediaId:N}/original/file.png";

    private string Full(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string[] CleanupQuarantineFiles()
    {
        var cleanupRoot = Full("temp/cleanup");
        return Directory.Exists(cleanupRoot)
            ? Directory.GetFiles(cleanupRoot, "*", SearchOption.AllDirectories)
            : [];
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FaultInjector : IMediaCleanupFaultInjector
    {
        internal int? FailPrepareMoveAt { get; init; }
        internal int? FailRestoreMoveAt { get; init; }
        internal int? FailDeleteAt { get; set; }

        public void BeforeMove(bool restoring, int index, string sourcePath, string destinationPath)
        {
            var failureIndex = restoring ? FailRestoreMoveAt : FailPrepareMoveAt;
            if (failureIndex == index)
            {
                throw new IOException("simulated cleanup move failure");
            }
        }

        public void BeforeDelete(int index, string path)
        {
            if (FailDeleteAt == index)
            {
                throw new IOException("simulated cleanup finalize failure");
            }
        }
    }
}
