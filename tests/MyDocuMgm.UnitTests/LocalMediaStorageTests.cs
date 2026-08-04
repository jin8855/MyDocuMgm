using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using MyDocuMgm.Infrastructure.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace MyDocuMgm.UnitTests;

public sealed class LocalMediaStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"MyDocuMgm-media-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("image.jpg", "image/jpeg", "jpg")]
    [InlineData("image.png", "image/png", "png")]
    [InlineData("image.webp", "image/webp", "webp")]
    public async Task ValidImage_IsFullyDecodedPreparedAndAtomicallyPromoted(
        string fileName,
        string mimeType,
        string format)
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();
        var contentId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var bytes = ImageBytes(format, 2, 3);

        var prepared = await storage.PrepareAsync(
            new MemoryStream(bytes),
            contentId,
            mediaId,
            fileName,
            mimeType,
            default);

        Assert.Equal((2, 3), (prepared.Width, prepared.Height));
        Assert.StartsWith($"media/{contentId:N}/{mediaId:N}/original/", prepared.RelativePath);
        Assert.DoesNotContain("image", prepared.StoredFileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(FullPath(prepared.TemporaryRelativePath)));
        Assert.False(File.Exists(FullPath(prepared.RelativePath)));

        await storage.PromoteAsync(prepared, default);

        Assert.False(File.Exists(FullPath(prepared.TemporaryRelativePath)));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(FullPath(prepared.RelativePath)));
    }

    [Fact]
    public async Task DeclaredMimeMustMatchExtension()
    {
        Directory.CreateDirectory(_root);
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 1, 1)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "image.webp",
                "image/png",
                default));
        Assert.Equal("MEDIA_MIME_MISMATCH", error.Code);
    }

    [Fact]
    public async Task SignatureMustMatchAllowedExtension()
    {
        Directory.CreateDirectory(_root);
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 1, 1)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "image.webp",
                "image/webp",
                default));
        Assert.Equal("MEDIA_SIGNATURE_INVALID", error.Code);
    }

    [Theory]
    [InlineData("image.gif", "image/gif")]
    [InlineData("image.heic", "image/heic")]
    [InlineData("image.svg", "image/svg+xml")]
    public async Task DeferredAndUnsafeFormatsAreRejected(string fileName, string mimeType)
    {
        Directory.CreateDirectory(_root);
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream([1, 2, 3]),
                Guid.NewGuid(),
                Guid.NewGuid(),
                fileName,
                mimeType,
                default));
        Assert.Equal("MEDIA_EXTENSION_NOT_ALLOWED", error.Code);
    }

    [Theory]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("png", "image/png")]
    [InlineData("webp", "image/webp")]
    public async Task TruncatedImageFailsContainerValidation(string format, string mimeType)
    {
        Directory.CreateDirectory(_root);
        var complete = ImageBytes(format, 4, 4);
        var truncated = complete[..Math.Max(1, complete.Length / 2)];
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(truncated),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"image.{format}",
                mimeType,
                default));
        Assert.Equal("MEDIA_SIGNATURE_INVALID", error.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ValidSignatureWithCorruptPayloadFailsFullDecode()
    {
        Directory.CreateDirectory(_root);
        var corrupt = ImageBytes("png", 4, 4);
        var idat = corrupt.AsSpan().IndexOf("IDAT"u8);
        Assert.True(idat > 0);
        corrupt[idat + 5] ^= 0x7F;
        var unrelatedPath = Path.Combine(_root, "unrelated.txt");
        await File.WriteAllTextAsync(unrelatedPath, "keep");

        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(corrupt),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "corrupt.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_DECODE_FAILED", error.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelatedPath));
    }

    [Fact]
    public async Task ExternalUrlInputIsRejectedWithoutCreatingTemporaryFiles()
    {
        Directory.CreateDirectory(_root);

        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 1, 1)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "https://example.test/image.png",
                "image/png",
                default));

        Assert.Equal("MEDIA_SOURCE_NAME_INVALID", error.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public void DefaultResourceLimitsMatchApprovedPolicy()
    {
        var options = new StorageOptions();

        Assert.Equal(20_971_520, options.MaxImageBytes);
        Assert.Equal(8192, options.MaxPixelWidth);
        Assert.Equal(8192, options.MaxPixelHeight);
        Assert.Equal(40_000_000, options.MaxTotalPixels);
    }

    [Fact]
    public async Task TrailingPolyglotPayloadIsRejected()
    {
        Directory.CreateDirectory(_root);
        var bytes = ImageBytes("png", 2, 2).Concat("<script>alert(1)</script>"u8.ToArray()).ToArray();
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(bytes),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "polyglot.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_SIGNATURE_INVALID", error.Code);
    }

    [Fact]
    public async Task ZeroByteFileIsRejected()
    {
        Directory.CreateDirectory(_root);
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "empty.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_SIGNATURE_INVALID", error.Code);
    }

    [Fact]
    public async Task FileAndPixelBoundariesAreEnforced()
    {
        Directory.CreateDirectory(_root);
        var bytes = ImageBytes("png", 4, 4);
        var sizeError = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage(maxBytes: bytes.Length - 1).PrepareAsync(
                new MemoryStream(bytes),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "large.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_FILE_TOO_LARGE", sizeError.Code);

        var pixelError = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage(maxWidth: 3).PrepareAsync(
                new MemoryStream(bytes),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "wide.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_DIMENSION_TOO_LARGE", pixelError.Code);

        var heightError = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage(maxHeight: 3).PrepareAsync(
                new MemoryStream(bytes),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "tall.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_DIMENSION_TOO_LARGE", heightError.Code);

        var totalPixelsError = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage(maxTotalPixels: 15).PrepareAsync(
                new MemoryStream(bytes),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "pixels.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_PIXEL_COUNT_TOO_LARGE", totalPixelsError.Code);
    }

    [Fact]
    public async Task ConfigurationCannotRaiseApprovedResourceLimits()
    {
        Directory.CreateDirectory(_root);
        var bytes = ImageBytes("png", 8193, 1);

        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage(
                    maxBytes: 30 * 1024 * 1024,
                    maxWidth: 9000,
                    maxHeight: 9000,
                    maxTotalPixels: 50_000_000)
                .PrepareAsync(
                    new MemoryStream(bytes),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "too-wide.png",
                    "image/png",
                    default));

        Assert.Equal("MEDIA_DIMENSION_TOO_LARGE", error.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("..\\escape.png")]
    [InlineData("\\\\server\\share\\image.png")]
    [InlineData("\\\\?\\C:\\image.png")]
    [InlineData("C:\\image.png")]
    public void PathEscapeAndRootedFormsAreRejected(string relativePath)
    {
        var root = Path.GetFullPath(_root);
        var error = Assert.Throws<MediaOperationException>(
            () => LocalMediaStorage.ResolveUnderRoot(root, relativePath));
        Assert.Equal("MEDIA_STORAGE_PATH_INVALID", error.Code);
    }

    [Theory]
    [InlineData("..\\image.png")]
    [InlineData("../image.png")]
    [InlineData("CON.png")]
    [InlineData("NUL.jpg")]
    public async Task UnsafeSourceNameIsRejected(string fileName)
    {
        Directory.CreateDirectory(_root);
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 1, 1)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                fileName,
                "image/png",
                default));
        Assert.Equal("MEDIA_SOURCE_NAME_INVALID", error.Code);
    }

    [Fact]
    public async Task MissingExplicitRootFailsClosed()
    {
        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 1, 1)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "image.png",
                "image/png",
                default));
        Assert.Equal("MEDIA_STORAGE_NOT_READY", error.Code);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task ThumbnailIsRegenerableAndOriginalRemainsByteIdentical()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();
        var bytes = ImageBytes("jpeg", 10, 6);
        var mediaId = Guid.NewGuid();
        var prepared = await storage.PrepareAsync(
            new MemoryStream(bytes),
            Guid.NewGuid(),
            mediaId,
            "photo.jpeg",
            "image/jpeg",
            default);
        await storage.PromoteAsync(prepared, default);

        await using (var first = (await storage.GetOrCreateThumbnailAsync(
                         mediaId,
                         prepared.RelativePath,
                         prepared.MimeType,
                         prepared.SizeBytes,
                         prepared.Sha256,
                         default)).Content)
        {
            Assert.True(first.Length > 0);
        }
        Assert.Equal(bytes, await File.ReadAllBytesAsync(FullPath(prepared.RelativePath)));

        var thumbnailPath = Directory.EnumerateFiles(
            Path.Combine(_root, "derived"),
            "*.webp",
            SearchOption.AllDirectories).Single();
        File.Delete(thumbnailPath);
        await using var regenerated = (await storage.GetOrCreateThumbnailAsync(
            mediaId,
            prepared.RelativePath,
            prepared.MimeType,
            prepared.SizeBytes,
            prepared.Sha256,
            default)).Content;
        Assert.True(regenerated.Length > 0);
    }

    [Fact]
    public async Task ThumbnailAppliesOrientationAndStripsExifAndGps()
    {
        Directory.CreateDirectory(_root);
        using var sourceImage = new Image<Rgba32>(10, 6, Color.CornflowerBlue);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        sourceImage.Metadata.ExifProfile = exif;
        using var source = new MemoryStream();
        sourceImage.Save(source, new JpegEncoder());
        var original = source.ToArray();
        var storage = CreateStorage();
        var mediaId = Guid.NewGuid();
        var prepared = await storage.PrepareAsync(
            new MemoryStream(original),
            Guid.NewGuid(),
            mediaId,
            "oriented.jpg",
            "image/jpeg",
            default);
        await storage.PromoteAsync(prepared, default);

        await using var thumbnail = (await storage.GetOrCreateThumbnailAsync(
            mediaId,
            prepared.RelativePath,
            prepared.MimeType,
            prepared.SizeBytes,
            prepared.Sha256,
            default)).Content;
        using var decoded = await Image.LoadAsync(thumbnail);

        Assert.Equal((6, 10), (decoded.Width, decoded.Height));
        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Equal(original, await File.ReadAllBytesAsync(FullPath(prepared.RelativePath)));
    }

    [Fact]
    public async Task CancellationRemovesTemporaryFileAndReturnsStableCode()
    {
        Directory.CreateDirectory(_root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<MediaOperationException>(() =>
            CreateStorage().PrepareAsync(
                new MemoryStream(ImageBytes("png", 2, 2)),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "cancel.png",
                "image/png",
                cancellation.Token));

        Assert.Equal("MEDIA_OPERATION_CANCELLED", error.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancelledReadinessRemovesProbeFile()
    {
        Directory.CreateDirectory(_root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var readiness = await CreateStorage().CheckReadinessAsync(cancellation.Token);

        Assert.False(readiness.IsReady);
        Assert.Equal("MEDIA_OPERATION_CANCELLED", readiness.Code);
        Assert.Empty(Directory.EnumerateFiles(_root, ".readiness-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ReconciliationReportsMissingOrphanAndStaleTempWithoutDeleting()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();
        var referenced = await storage.PrepareAsync(
            new MemoryStream(ImageBytes("png", 2, 2)),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "referenced.png",
            "image/png",
            default);
        await storage.PromoteAsync(referenced, default);
        var orphan = await storage.PrepareAsync(
            new MemoryStream(ImageBytes("png", 3, 3)),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "orphan.png",
            "image/png",
            default);
        await storage.PromoteAsync(orphan, default);
        var stalePath = FullPath("temp/stale.part");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        await File.WriteAllBytesAsync(stalePath, [1]);
        File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddHours(-2));
        File.Delete(FullPath(referenced.RelativePath));

        var report = await storage.ReconcileAsync(
            [
                new MediaStorageReference(
                    Guid.NewGuid(),
                    referenced.RelativePath,
                    referenced.MimeType,
                    referenced.SizeBytes,
                    referenced.Sha256)
            ],
            default);

        Assert.Equal(1, report.MissingCount);
        Assert.Equal(1, report.OrphanCount);
        Assert.Equal(1, report.StaleTemporaryCount);
        Assert.True(File.Exists(FullPath(orphan.RelativePath)));
        Assert.True(File.Exists(stalePath));
    }

    [Fact]
    public async Task IntegrityCheckReportsEscapingDatabasePathWithoutOpeningIt()
    {
        Directory.CreateDirectory(_root);
        var result = await CreateStorage().VerifyAsync(
            "../outside.png",
            "image/png",
            1,
            new string('A', 64),
            default);

        Assert.False(result.IsValid);
        Assert.Equal("MEDIA_STORAGE_PATH_INVALID", result.Code);
    }

    [Fact]
    public void ReparsePointRootIsRejected_WhenPlatformAllowsCreatingOne()
    {
        var target = _root + "-target";
        var link = _root + "-link";
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        var error = Assert.Throws<MediaOperationException>(
            () => LocalMediaStorage.EnsureReadyRoot(link));
        Assert.Equal("MEDIA_STORAGE_PATH_INVALID", error.Code);
    }

    [Fact]
    public async Task DescendantReparsePointIsRejectedBeforeCreatingOutsideDirectories()
    {
        var outside = _root + "-outside";
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(outside);
        var storage = CreateStorage();
        var prepared = await storage.PrepareAsync(
            new MemoryStream(ImageBytes("png", 1, 1)),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "safe.png",
            "image/png",
            default);
        var mediaLink = Path.Combine(_root, "media");
        try
        {
            Directory.CreateSymbolicLink(mediaLink, outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        var error = await Assert.ThrowsAsync<MediaOperationException>(
            () => storage.PromoteAsync(prepared, default));
        Assert.Equal("MEDIA_STORAGE_PATH_INVALID", error.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    public void Dispose()
    {
        foreach (var path in new[] { _root, _root + "-link", _root + "-target", _root + "-outside" })
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private LocalMediaStorage CreateStorage(
        long maxBytes = 20 * 1024 * 1024,
        int maxWidth = 8192,
        int maxHeight = 8192,
        long maxTotalPixels = 40_000_000) =>
        new(Options.Create(new StorageOptions
        {
            RootPath = _root,
            MaxImageBytes = maxBytes,
            MaxPixelWidth = maxWidth,
            MaxPixelHeight = maxHeight,
            MaxTotalPixels = maxTotalPixels
        }));

    private string FullPath(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static byte[] ImageBytes(string format, int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Color.CornflowerBlue);
        using var stream = new MemoryStream();
        switch (format.ToLowerInvariant())
        {
            case "jpg":
            case "jpeg":
                image.Save(stream, new JpegEncoder());
                break;
            case "webp":
                image.Save(stream, new WebpEncoder());
                break;
            default:
                image.Save(stream, new PngEncoder());
                break;
        }

        return stream.ToArray();
    }
}
