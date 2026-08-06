using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace MyDocuMgm.Infrastructure.Storage;

public sealed class LocalMediaStorage(
    IOptions<StorageOptions> options,
    IMediaDiagnostics? diagnostics = null) : IMediaStorage, IMediaStorageReadiness, IMediaCleanupStorage
{
    internal IMediaCleanupFaultInjector CleanupFaultInjector { get; init; } = NoopMediaCleanupFaultInjector.Instance;
    private const long ApprovedMaxFileBytes = 20_971_520;
    private const int ApprovedMaxPixelWidth = 8192;
    private const int ApprovedMaxPixelHeight = 8192;
    private const long ApprovedMaxTotalPixels = 40_000_000;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ThumbnailLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, AllowedFormat> FormatsByExtension =
        new Dictionary<string, AllowedFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new("JPEG", "image/jpeg", ".jpg"),
            [".jpeg"] = new("JPEG", "image/jpeg", ".jpg"),
            [".png"] = new("PNG", "image/png", ".png"),
            [".webp"] = new("WEBP", "image/webp", ".webp")
        };

    private readonly StorageOptions _options = options.Value;

    public async Task<MediaStorageReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        string? readinessPath = null;
        try
        {
            var root = EnsureReadyRoot(_options.RootPath);
            var relativePath = $"temp/.readiness-{Guid.NewGuid():N}.tmp";
            readinessPath = ResolveUnderRoot(root, relativePath);
            EnsureSafeDirectory(root, Path.GetDirectoryName(readinessPath)!);
            await using (var stream = new FileStream(
                             readinessPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1,
                             FileOptions.Asynchronous))
            {
                await stream.WriteAsync(new byte[] { 0 }, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            DeleteFileIfPresent(readinessPath);
            return new(true, "MEDIA_STORAGE_READY");
        }
        catch (MediaOperationException exception)
        {
            return new(false, exception.Code);
        }
        catch (OperationCanceledException)
        {
            return new(false, "MEDIA_OPERATION_CANCELLED");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, "MEDIA_STORAGE_NOT_WRITABLE");
        }
        finally
        {
            if (readinessPath is not null && !TryDelete(readinessPath))
            {
                diagnostics?.Record("MEDIA_READINESS_TEMP_DELETE_FAILED", null);
            }
        }
    }

    public async Task<PreparedMedia> PrepareAsync(
        Stream source,
        Guid contentId,
        Guid mediaId,
        string originalFileName,
        string declaredMimeType,
        CancellationToken cancellationToken)
    {
        var allowed = ValidateSourceNameAndMime(originalFileName, declaredMimeType);
        var root = EnsureReadyRoot(_options.RootPath);
        var temporaryRelativePath = $"temp/{Guid.NewGuid():N}.part";
        var temporaryPath = ResolveUnderRoot(root, temporaryRelativePath);
        EnsureSafeDirectory(root, Path.GetDirectoryName(temporaryPath)!);

        try
        {
            var (sizeBytes, sha256) = await CopyBoundedAsync(source, temporaryPath, cancellationToken);
            RejectFileReparsePoint(temporaryPath);
            await ValidateContainerBoundaryAsync(temporaryPath, allowed, cancellationToken);
            var imageInfo = await ValidateDecodedImageAsync(
                temporaryPath,
                allowed,
                declaredMimeType,
                enforceDimensions: true,
                cancellationToken);

            var storedFileName = $"{Guid.NewGuid():N}{allowed.CanonicalExtension}";
            var relativePath =
                $"media/{contentId:N}/{mediaId:N}/original/{storedFileName}";

            return new PreparedMedia(
                temporaryRelativePath,
                storedFileName,
                relativePath,
                allowed.MimeType,
                sizeBytes,
                sha256,
                imageInfo.Width,
                imageInfo.Height);
        }
        catch (OperationCanceledException)
        {
            if (!TryDelete(temporaryPath))
            {
                diagnostics?.Record("MEDIA_COMPENSATION_TEMP_DELETE_FAILED", contentId, mediaId);
            }
            throw new MediaOperationException(
                "MEDIA_OPERATION_CANCELLED",
                "이미지 작업이 취소되었습니다.");
        }
        catch
        {
            if (!TryDelete(temporaryPath))
            {
                diagnostics?.Record("MEDIA_COMPENSATION_TEMP_DELETE_FAILED", contentId, mediaId);
            }
            throw;
        }
    }

    public Task PromoteAsync(PreparedMedia prepared, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new MediaOperationException(
                "MEDIA_OPERATION_CANCELLED",
                "이미지 승격 작업이 취소되었습니다.");
        }
        var root = EnsureReadyRoot(_options.RootPath);
        var temporaryPath = ResolveUnderRoot(root, prepared.TemporaryRelativePath);
        var finalPath = ResolveUnderRoot(root, prepared.RelativePath);
        EnsureSafeDirectory(root, Path.GetDirectoryName(finalPath)!);

        if (!File.Exists(temporaryPath))
        {
            throw new MediaOperationException(
                "MEDIA_TEMP_FILE_MISSING",
                "승격할 임시 이미지가 없습니다.");
        }
        RejectFileReparsePoint(temporaryPath);

        try
        {
            File.Move(temporaryPath, finalPath, overwrite: false);
            return Task.CompletedTask;
        }
        catch (IOException)
        {
            throw new MediaOperationException(
                "MEDIA_PROMOTION_FAILED",
                "이미지 원본을 최종 위치로 승격하지 못했습니다.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_ACCESS_DENIED",
                "이미지 저장소에 접근할 수 없습니다.");
        }
    }

    public Task DiscardPreparedAsync(PreparedMedia prepared, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureReadyRoot(_options.RootPath);
        DeleteFileIfPresent(ResolveUnderRoot(root, prepared.TemporaryRelativePath));
        return Task.CompletedTask;
    }

    public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureReadyRoot(_options.RootPath);
        DeleteFileIfPresent(ResolveUnderRoot(root, relativePath));
        return Task.CompletedTask;
    }

    public Task<MediaCleanupStorageState> InspectAsync(
        Guid contentId,
        Guid mediaId,
        string relativePath,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var (_, path) = ResolveCleanupTarget(contentId, mediaId, relativePath, storedFileName);
            if (!File.Exists(path))
            {
                return Task.FromResult(new MediaCleanupStorageState(false, true, "MEDIA_FILE_ALREADY_ABSENT"));
            }

            RejectFileReparsePoint(path);
            return Task.FromResult(new MediaCleanupStorageState(true, true, "MEDIA_FILE_READY_FOR_CLEANUP"));
        }
        catch (MediaOperationException exception)
        {
            return Task.FromResult(new MediaCleanupStorageState(false, false, exception.Code));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(new MediaCleanupStorageState(false, false, "MEDIA_STORAGE_READ_FAILED"));
        }
    }

    public Task<PreparedMediaCleanup> PrepareDeleteAsync(
        Guid contentId,
        Guid mediaId,
        string relativePath,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (root, originalPath) = ResolveCleanupTarget(contentId, mediaId, relativePath, storedFileName);
        var originalAbsent = !File.Exists(originalPath);
        var sourcePaths = new List<string>();
        if (!originalAbsent)
        {
            RejectFileReparsePoint(originalPath);
            sourcePaths.Add(originalPath);
        }

        var thumbnailDirectory = ResolveUnderRoot(root, $"derived/thumbnails/{mediaId:N}");
        if (Directory.Exists(thumbnailDirectory))
        {
            sourcePaths.AddRange(EnumerateFilesSafely(root, thumbnailDirectory));
        }

        var quarantineRootRelative = $"temp/cleanup/{Guid.NewGuid():N}";
        var moved = new List<QuarantinedMediaFile>(sourcePaths.Count);
        try
        {
            for (var index = 0; index < sourcePaths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePath = sourcePaths[index];
                var sourceRelative = NormalizeRelative(Path.GetRelativePath(root, sourcePath));
                var quarantineRelative = $"{quarantineRootRelative}/{index:D4}-{Path.GetFileName(sourcePath)}";
                var quarantinePath = ResolveUnderRoot(root, quarantineRelative);
                EnsureSafeDirectory(root, Path.GetDirectoryName(quarantinePath)!);
                CleanupFaultInjector.BeforeMove(restoring: false, index, sourcePath, quarantinePath);
                File.Move(sourcePath, quarantinePath, overwrite: false);
                moved.Add(new QuarantinedMediaFile(sourceRelative, quarantineRelative));
            }

            return Task.FromResult(new PreparedMediaCleanup(moved, originalAbsent));
        }
        catch (Exception exception)
        {
            try
            {
                RestoreMovedFiles(root, moved);
            }
            catch (Exception restoreException)
            {
                diagnostics?.Record("MEDIA_CLEANUP_PREPARE_RESTORE_FAILED", null, mediaId);
                throw new MediaOperationException(
                    "MEDIA_CLEANUP_PREPARE_RESTORE_FAILED",
                    $"삭제 준비 실패 후 이동한 파일의 복원에도 실패했습니다: {restoreException.GetType().Name}");
            }

            if (exception is MediaOperationException)
            {
                throw;
            }

            throw new MediaOperationException(
                "MEDIA_CLEANUP_PREPARE_FAILED",
                "미디어 파일을 안전한 삭제 대기 위치로 옮기지 못했습니다.");
        }
    }

    public Task RestoreAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureReadyRoot(_options.RootPath);
        try
        {
            RestoreMovedFiles(root, cleanup.Files.Reverse());
            return Task.CompletedTask;
        }
        catch (MediaOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_RESTORE_FAILED",
                "DB 삭제 실패 후 미디어 파일을 원래 위치로 복원하지 못했습니다.");
        }
    }

    public Task CommitAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureReadyRoot(_options.RootPath);
        try
        {
            var fileIndex = 0;
            foreach (var file in cleanup.Files)
            {
                var quarantinePath = ResolveUnderRoot(root, file.QuarantineRelativePath);
                RejectReparsePoints(root, Path.GetDirectoryName(quarantinePath)!);
                RejectFileReparsePoint(quarantinePath);
                CleanupFaultInjector.BeforeDelete(fileIndex, quarantinePath);
                DeleteFileIfPresent(quarantinePath);
                fileIndex++;
            }

            foreach (var directory in cleanup.Files
                         .Select(file => Path.GetDirectoryName(ResolveUnderRoot(root, file.QuarantineRelativePath)))
                         .Where(directory => directory is not null)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    RejectReparsePoints(root, directory);
                    Directory.Delete(directory);
                }
            }

            return Task.CompletedTask;
        }
        catch (MediaOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_FINALIZE_FAILED",
                "격리한 미디어 파일을 최종 삭제하지 못했습니다.");
        }
    }

    public Task<MediaBinary> OpenOriginalAsync(
        string relativePath,
        string mimeType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureReadyRoot(_options.RootPath);
        var path = ResolveUnderRoot(root, relativePath);
        RejectReparsePoints(root, Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            throw new MediaOperationException("MEDIA_FILE_MISSING", "이미지 원본이 없습니다.");
        }
        RejectFileReparsePoint(path);

        try
        {
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(new MediaBinary(stream, mimeType));
        }
        catch (UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_ACCESS_DENIED",
                "이미지 원본에 접근할 수 없습니다.");
        }
    }

    public async Task<MediaBinary> GetOrCreateThumbnailAsync(
        Guid mediaId,
        string relativePath,
        string expectedMimeType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var integrity = await VerifyAsync(
            relativePath,
            expectedMimeType,
            expectedSizeBytes,
            expectedSha256,
            cancellationToken);
        if (!integrity.IsValid)
        {
            throw new MediaOperationException(integrity.Code, "이미지 원본 무결성 검증에 실패했습니다.");
        }

        var root = EnsureReadyRoot(_options.RootPath);
        var hashPrefix = expectedSha256[..Math.Min(16, expectedSha256.Length)].ToLowerInvariant();
        var thumbnailRelativePath =
            $"derived/thumbnails/{mediaId:N}/{hashPrefix}-{_options.ThumbnailMaxPixels}.webp";
        var thumbnailPath = ResolveUnderRoot(root, thumbnailRelativePath);
        var gate = ThumbnailLocks.GetOrAdd(thumbnailPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(thumbnailPath))
            {
                var originalPath = ResolveUnderRoot(root, relativePath);
                RejectFileReparsePoint(originalPath);
                EnsureSafeDirectory(root, Path.GetDirectoryName(thumbnailPath)!);
                var temporaryThumbnailPath = thumbnailPath + $".{Guid.NewGuid():N}.part";
                try
                {
                    await using var original = new FileStream(
                        originalPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81_920,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var image = await Image.LoadAsync(original, cancellationToken);
                    image.Mutate(context => context.AutoOrient());
                    if (image.Width > _options.ThumbnailMaxPixels ||
                        image.Height > _options.ThumbnailMaxPixels)
                    {
                        image.Mutate(context => context.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(_options.ThumbnailMaxPixels, _options.ThumbnailMaxPixels)
                        }));
                    }
                    image.Metadata.ExifProfile = null;
                    image.Metadata.IptcProfile = null;
                    image.Metadata.XmpProfile = null;

                    await image.SaveAsWebpAsync(
                        temporaryThumbnailPath,
                        new WebpEncoder { Quality = 82 },
                        cancellationToken);
                    File.Move(temporaryThumbnailPath, thumbnailPath, overwrite: false);
                }
                catch (OperationCanceledException)
                {
                    throw new MediaOperationException(
                        "MEDIA_OPERATION_CANCELLED",
                        "썸네일 생성이 취소되었습니다.");
                }
                catch (MediaOperationException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is IOException or
                                                  UnauthorizedAccessException or
                                                  InvalidImageContentException or
                                                  UnknownImageFormatException)
                {
                    throw new MediaOperationException(
                        "MEDIA_THUMBNAIL_GENERATION_FAILED",
                        "썸네일을 생성하지 못했습니다.");
                }
                finally
                {
                    if (!TryDelete(temporaryThumbnailPath))
                    {
                        diagnostics?.Record("MEDIA_THUMBNAIL_TEMP_DELETE_FAILED", null, mediaId);
                    }
                }
            }

            Stream result = new FileStream(
                thumbnailPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new MediaBinary(result, "image/webp");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<MediaIntegrityResult> VerifyAsync(
        string relativePath,
        string expectedMimeType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            var root = EnsureReadyRoot(_options.RootPath);
            var path = ResolveUnderRoot(root, relativePath);
            RejectReparsePoints(root, Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                return new(false, "MEDIA_FILE_MISSING");
            }
            RejectFileReparsePoint(path);

            var file = new FileInfo(path);
            if (file.Length != expectedSizeBytes)
            {
                return new(false, "MEDIA_INTEGRITY_SIZE_MISMATCH");
            }

            await using (var hashStream = new FileStream(
                             path,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81_920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken));
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(actualHash),
                        Convert.FromHexString(expectedSha256)))
                {
                    return new(false, "MEDIA_INTEGRITY_HASH_MISMATCH");
                }
            }

            var extension = Path.GetExtension(path);
            if (!FormatsByExtension.TryGetValue(extension, out var allowed))
            {
                return new(false, "MEDIA_EXTENSION_NOT_ALLOWED");
            }

            await ValidateDecodedImageAsync(
                path,
                allowed,
                expectedMimeType,
                enforceDimensions: true,
                cancellationToken);
            return new(true, "MEDIA_INTEGRITY_OK");
        }
        catch (MediaOperationException exception)
        {
            return new(false, exception.Code);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, "MEDIA_STORAGE_READ_FAILED");
        }
        catch (FormatException)
        {
            return new(false, "MEDIA_INTEGRITY_HASH_MISMATCH");
        }
    }

    public async Task<MediaReconciliationReport> ReconcileAsync(
        IReadOnlyList<MediaStorageReference> references,
        CancellationToken cancellationToken)
    {
        var issues = new List<MediaReconciliationIssue>();
        string root;
        try
        {
            root = EnsureReadyRoot(_options.RootPath);
        }
        catch (MediaOperationException exception)
        {
            issues.Add(new(exception.Code, null));
            return new(
                references.Count,
                0,
                0,
                references.Count,
                0,
                0,
                issues);
        }

        var missing = 0;
        var mismatched = 0;
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await VerifyAsync(
                reference.RelativePath,
                reference.MimeType,
                reference.SizeBytes,
                reference.Sha256,
                cancellationToken);
            if (result.IsValid)
            {
                continue;
            }

            if (result.Code == "MEDIA_FILE_MISSING")
            {
                missing++;
            }
            else
            {
                mismatched++;
            }

            issues.Add(new(result.Code, reference.MediaId));
        }

        var referencedPaths = references
            .Select(reference => NormalizeRelative(reference.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphanCount = 0;
        var mediaRoot = ResolveUnderRoot(root, "media");
        if (Directory.Exists(mediaRoot))
        {
            RejectReparsePoints(root, mediaRoot);
            foreach (var file in EnumerateFilesSafely(root, mediaRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = NormalizeRelative(Path.GetRelativePath(root, file));
                if (relative.Contains("/original/", StringComparison.OrdinalIgnoreCase) &&
                    !referencedPaths.Contains(relative))
                {
                    orphanCount++;
                    issues.Add(new("MEDIA_ORPHAN_FILE", null));
                }
            }
        }

        var staleTemporaryCount = 0;
        var tempRoot = ResolveUnderRoot(root, "temp");
        if (Directory.Exists(tempRoot))
        {
            RejectReparsePoints(root, tempRoot);
            var staleBeforeUtc = DateTime.UtcNow.AddHours(-1);
            foreach (var file in Directory.EnumerateFiles(tempRoot, "*.part", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.GetLastWriteTimeUtc(file) < staleBeforeUtc)
                {
                    staleTemporaryCount++;
                    issues.Add(new("MEDIA_STALE_TEMPORARY_FILE", null));
                }
            }
        }

        return new(
            references.Count,
            references.Count,
            missing,
            mismatched,
            orphanCount,
            staleTemporaryCount,
            issues);
    }

    internal static string EnsureReadyRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathFullyQualified(configuredRoot))
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_NOT_READY",
                "이미지 저장소 루트는 명시적인 절대경로여야 합니다.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        if (!Directory.Exists(root))
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_NOT_READY",
                "이미지 저장소 루트가 초기화되지 않았습니다.");
        }

        RejectReparsePoints(root, root);
        return root;
    }

    internal static string ResolveUnderRoot(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith(@"\\", StringComparison.Ordinal) ||
            relativePath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            relativePath.Contains('\0'))
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_PATH_INVALID",
                "허용되지 않은 이미지 저장 경로입니다.");
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, normalized));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, pathComparison))
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_PATH_INVALID",
                "이미지 저장 경로가 저장소 루트를 벗어납니다.");
        }

        return candidate;
    }

    private (string Root, string Path) ResolveCleanupTarget(
        Guid contentId,
        Guid mediaId,
        string relativePath,
        string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            !string.Equals(Path.GetFileName(storedFileName), storedFileName, StringComparison.Ordinal) ||
            IsReservedDeviceName(storedFileName))
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_IDENTITY_INVALID",
                "미디어 파일 식별 정보가 안전하지 않아 삭제할 수 없습니다.");
        }

        var root = EnsureReadyRoot(_options.RootPath);
        var expectedRelativePath = $"media/{contentId:N}/{mediaId:N}/original/{storedFileName}";
        if (!string.Equals(
                NormalizeRelative(relativePath),
                expectedRelativePath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_IDENTITY_INVALID",
                "DB의 미디어 소유권과 삭제 대상 경로가 일치하지 않습니다.");
        }

        var path = ResolveUnderRoot(root, relativePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFileName(path), storedFileName, comparison))
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_IDENTITY_INVALID",
                "DB의 저장 파일명과 삭제 대상 파일명이 일치하지 않습니다.");
        }

        RejectReparsePoints(root, Path.GetDirectoryName(path)!);
        if (Directory.Exists(path))
        {
            throw new MediaOperationException(
                "MEDIA_CLEANUP_TARGET_INVALID",
                "미디어 삭제 대상이 단일 파일이 아닙니다.");
        }

        return (root, path);
    }

    private void RestoreMovedFiles(string root, IEnumerable<QuarantinedMediaFile> files)
    {
        var index = 0;
        foreach (var file in files)
        {
            var quarantinePath = ResolveUnderRoot(root, file.QuarantineRelativePath);
            var originalPath = ResolveUnderRoot(root, file.OriginalRelativePath);
            RejectReparsePoints(root, Path.GetDirectoryName(quarantinePath)!);
            if (!File.Exists(quarantinePath))
            {
                continue;
            }

            RejectFileReparsePoint(quarantinePath);
            if (File.Exists(originalPath) || Directory.Exists(originalPath))
            {
                throw new MediaOperationException(
                    "MEDIA_CLEANUP_RESTORE_CONFLICT",
                    "복원 대상 위치에 다른 파일 또는 폴더가 있어 복원할 수 없습니다.");
            }

            EnsureSafeDirectory(root, Path.GetDirectoryName(originalPath)!);
            CleanupFaultInjector.BeforeMove(restoring: true, index, quarantinePath, originalPath);
            File.Move(quarantinePath, originalPath, overwrite: false);
            index++;
        }
    }

    private AllowedFormat ValidateSourceNameAndMime(string originalFileName, string declaredMimeType)
    {
        if (string.IsNullOrWhiteSpace(originalFileName) ||
            Path.IsPathRooted(originalFileName) ||
            Path.GetFileName(originalFileName) != originalFileName ||
            originalFileName.Any(char.IsControl) ||
            originalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            IsReservedDeviceName(originalFileName))
        {
            throw new MediaOperationException(
                "MEDIA_SOURCE_NAME_INVALID",
                "원본 파일 이름이 안전하지 않습니다.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (!FormatsByExtension.TryGetValue(extension, out var allowed))
        {
            throw new MediaOperationException(
                "MEDIA_EXTENSION_NOT_ALLOWED",
                "JPEG, PNG, WebP 파일만 허용됩니다.");
        }

        if (!string.Equals(allowed.MimeType, declaredMimeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new MediaOperationException(
                "MEDIA_MIME_MISMATCH",
                "선언된 MIME 형식이 파일 확장자와 일치하지 않습니다.");
        }

        return allowed;
    }

    private async Task<(long SizeBytes, string Sha256)> CopyBoundedAsync(
        Stream source,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81_920];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > Math.Min(_options.MaxImageBytes, ApprovedMaxFileBytes))
                {
                    throw new MediaOperationException(
                        "MEDIA_FILE_TOO_LARGE",
                        "이미지 파일은 20 MiB를 초과할 수 없습니다.");
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (total == 0)
            {
                throw new MediaOperationException(
                    "MEDIA_SIGNATURE_INVALID",
                    "빈 파일은 이미지로 등록할 수 없습니다.");
            }

            await destination.FlushAsync(cancellationToken);
            return (total, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch (MediaOperationException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_ACCESS_DENIED",
                "이미지 저장소에 쓸 수 없습니다.");
        }
        catch (IOException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_WRITE_FAILED",
                "이미지 임시 파일을 저장하지 못했습니다.");
        }
    }

    private async Task<ImageInfo> ValidateDecodedImageAsync(
        string path,
        AllowedFormat allowed,
        string declaredMimeType,
        bool enforceDimensions,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var detectedFormat = await Image.DetectFormatAsync(stream, cancellationToken)
                ?? throw new MediaOperationException(
                    "MEDIA_SIGNATURE_INVALID",
                    "지원하는 이미지 시그니처를 찾을 수 없습니다.");

            if (!string.Equals(detectedFormat.Name, allowed.ImageSharpName, StringComparison.OrdinalIgnoreCase))
            {
                throw new MediaOperationException(
                    "MEDIA_SIGNATURE_INVALID",
                    "파일 확장자와 이미지 시그니처가 일치하지 않습니다.");
            }

            if (!string.Equals(allowed.MimeType, declaredMimeType, StringComparison.OrdinalIgnoreCase))
            {
                throw new MediaOperationException(
                    "MEDIA_MIME_MISMATCH",
                    "MIME 형식과 이미지 시그니처가 일치하지 않습니다.");
            }

            stream.Position = 0;
            var info = await Image.IdentifyAsync(stream, cancellationToken)
                ?? throw new MediaOperationException(
                    "MEDIA_DECODE_FAILED",
                    "이미지 크기를 해석할 수 없습니다.");
            if (enforceDimensions &&
                (info.Width > Math.Min(_options.MaxPixelWidth, ApprovedMaxPixelWidth) ||
                 info.Height > Math.Min(_options.MaxPixelHeight, ApprovedMaxPixelHeight)))
            {
                throw new MediaOperationException(
                    "MEDIA_DIMENSION_TOO_LARGE",
                    "이미지 가로 또는 세로 크기가 허용 한도를 초과합니다.");
            }

            if (enforceDimensions &&
                (long)info.Width * info.Height >
                Math.Min(_options.MaxTotalPixels, ApprovedMaxTotalPixels))
            {
                throw new MediaOperationException(
                    "MEDIA_PIXEL_COUNT_TOO_LARGE",
                    "이미지 전체 픽셀 수가 허용 한도를 초과합니다.");
            }

            stream.Position = 0;
            using var image = await Image.LoadAsync(stream, cancellationToken);
            return info;
        }
        catch (MediaOperationException)
        {
            throw;
        }
        catch (UnknownImageFormatException)
        {
            throw new MediaOperationException(
                "MEDIA_SIGNATURE_INVALID",
                "지원하는 이미지 시그니처를 찾을 수 없습니다.");
        }
        catch (InvalidImageContentException)
        {
            throw new MediaOperationException(
                "MEDIA_DECODE_FAILED",
                "이미지 파일이 손상되었거나 완전하게 디코딩되지 않습니다.");
        }
        catch (NotSupportedException)
        {
            throw new MediaOperationException(
                "MEDIA_DECODE_FAILED",
                "지원하지 않는 이미지 인코딩입니다.");
        }
    }

    private static async Task ValidateContainerBoundaryAsync(
        string path,
        AllowedFormat allowed,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        try
        {
            switch (allowed.ImageSharpName)
            {
                case "PNG":
                await ValidatePngBoundaryAsync(stream, cancellationToken);
                break;
                case "JPEG":
                if (stream.Length < 4)
                {
                    throw SignatureError();
                }

                var jpegBoundary = new byte[2];
                await stream.ReadExactlyAsync(jpegBoundary, cancellationToken);
                if (jpegBoundary[0] != 0xFF || jpegBoundary[1] != 0xD8)
                {
                    throw SignatureError();
                }

                stream.Position = stream.Length - 2;
                await stream.ReadExactlyAsync(jpegBoundary, cancellationToken);
                if (jpegBoundary[0] != 0xFF || jpegBoundary[1] != 0xD9)
                {
                    throw SignatureError();
                }

                break;
                case "WEBP":
                if (stream.Length < 12)
                {
                    throw SignatureError();
                }

                var webpHeader = new byte[12];
                await stream.ReadExactlyAsync(webpHeader, cancellationToken);
                if (!webpHeader.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
                    !webpHeader.AsSpan(8, 4).SequenceEqual("WEBP"u8) ||
                    BinaryPrimitives.ReadUInt32LittleEndian(webpHeader.AsSpan(4, 4)) + 8 != stream.Length)
                {
                    throw SignatureError();
                }

                break;
                default:
                    throw SignatureError();
            }
        }
        catch (EndOfStreamException)
        {
            throw new MediaOperationException(
                "MEDIA_SIGNATURE_INVALID",
                "이미지 컨테이너가 완전하지 않습니다.");
        }
    }

    private static async Task ValidatePngBoundaryAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var signature = new byte[8];
        await stream.ReadExactlyAsync(signature, cancellationToken);
        if (!signature.AsSpan().SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            throw SignatureError();
        }

        var chunkHeader = new byte[8];
        while (stream.Position < stream.Length)
        {
            await stream.ReadExactlyAsync(chunkHeader, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader.AsSpan(0, 4));
            if (length > int.MaxValue || stream.Position + length + 4 > stream.Length)
            {
                throw SignatureError();
            }

            stream.Position += length + 4;
            if (chunkHeader.AsSpan(4, 4).SequenceEqual("IEND"u8))
            {
                if (length != 0 || stream.Position != stream.Length)
                {
                    throw SignatureError();
                }

                return;
            }
        }

        throw SignatureError();
    }

    private static MediaOperationException SignatureError() =>
        new(
            "MEDIA_SIGNATURE_INVALID",
            "이미지 시그니처 또는 컨테이너 경계가 올바르지 않습니다.");

    private static void EnsureSafeDirectory(string root, string directory)
    {
        var existingAncestor = new DirectoryInfo(directory);
        while (!existingAncestor.Exists)
        {
            existingAncestor = existingAncestor.Parent
                ?? throw new MediaOperationException(
                    "MEDIA_STORAGE_PATH_INVALID",
                    "이미지 저장 경로의 기존 상위 폴더를 확인할 수 없습니다.");
        }

        RejectReparsePoints(root, existingAncestor.FullName);
        Directory.CreateDirectory(directory);
        RejectReparsePoints(root, directory);
    }

    private static void RejectReparsePoints(string root, string targetDirectory)
    {
        var current = new DirectoryInfo(targetDirectory);
        var rootFullName = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        while (current is not null &&
               current.FullName.StartsWith(rootFullName, pathComparison))
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new MediaOperationException(
                    "MEDIA_STORAGE_PATH_INVALID",
                    "재분석 지점을 통과하는 이미지 저장 경로는 허용되지 않습니다.");
            }

            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(current.FullName),
                    rootFullName,
                    pathComparison))
            {
                break;
            }

            current = current.Parent;
        }
    }

    private static bool IsReservedDeviceName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd(' ', '.');
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                stem[3] is >= '1' and <= '9');
    }

    private static string NormalizeRelative(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteFileIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_ACCESS_DENIED",
                "이미지 저장소 파일을 삭제할 수 없습니다.");
        }
        catch (IOException)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_DELETE_FAILED",
                "이미지 저장소 파일을 삭제하지 못했습니다.");
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, string startDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(startDirectory);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            RejectReparsePoints(root, directory);
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                RejectFileReparsePoint(file);
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                RejectReparsePoints(root, child);
                pending.Push(child);
            }
        }
    }

    private static void RejectFileReparsePoint(string path)
    {
        if (File.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new MediaOperationException(
                "MEDIA_STORAGE_PATH_INVALID",
                "재분석 지점인 이미지 파일은 허용되지 않습니다.");
        }
    }

    private sealed record AllowedFormat(
        string ImageSharpName,
        string MimeType,
        string CanonicalExtension);
}

internal interface IMediaCleanupFaultInjector
{
    void BeforeMove(bool restoring, int index, string sourcePath, string destinationPath);

    void BeforeDelete(int index, string path);
}

internal sealed class NoopMediaCleanupFaultInjector : IMediaCleanupFaultInjector
{
    internal static NoopMediaCleanupFaultInjector Instance { get; } = new();

    private NoopMediaCleanupFaultInjector()
    {
    }

    public void BeforeMove(bool restoring, int index, string sourcePath, string destinationPath)
    {
    }

    public void BeforeDelete(int index, string path)
    {
    }
}
