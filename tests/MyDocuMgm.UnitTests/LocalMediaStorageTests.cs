using Microsoft.Extensions.Options;
using MyDocuMgm.Infrastructure.Storage;

namespace MyDocuMgm.UnitTests;

public sealed class LocalMediaStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"MyDocuMgm-{Guid.NewGuid():N}");

    [Fact]
    public async Task ValidPng_UsesGuidNameAndRecordsDimensions()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();

        var first = await storage.StoreAsync(new MemoryStream(Png(2, 3)), "same.png", "image/png", default);
        var second = await storage.StoreAsync(new MemoryStream(Png(2, 3)), "same.png", "image/png", default);

        Assert.Equal("image/png", first.MimeType);
        Assert.Equal((2, 3), (first.Width, first.Height));
        Assert.NotEqual(first.StoredFileName, second.StoredFileName);
        Assert.Equal(32 + 4, first.StoredFileName.Length);
        Assert.DoesNotContain("same", first.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeclaredMimeMustMatchSignature()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.StoreAsync(new MemoryStream(Png(1, 1)), "image.webp", "image/webp", default));
    }

    [Fact]
    public async Task OriginalExtensionMustMatchSignature()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.StoreAsync(new MemoryStream(Png(1, 1)), "image.txt", "image/png", default));
    }

    [Fact]
    public async Task SizeBoundaryIsEnforcedBeforeWrite()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage(maxBytes: 4);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.StoreAsync(new MemoryStream(new byte[5]), "large.png", "image/png", default));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("..\\escape.png")]
    public void TraversalCannotResolveOutsideRoot(string relativePath)
    {
        var root = Path.GetFullPath(_root);
        Assert.Throws<InvalidDataException>(() => LocalMediaStorage.ResolveUnderRoot(root, relativePath));
    }

    [Fact]
    public void AbsolutePathCannotResolveAsRelativeMediaPath()
    {
        var root = Path.GetFullPath(_root);
        Assert.Throws<InvalidDataException>(() => LocalMediaStorage.ResolveUnderRoot(root, Path.Combine(root, "image.png")));
    }

    [Fact]
    public async Task OriginalFileNameCannotContainAPath()
    {
        Directory.CreateDirectory(_root);
        var storage = CreateStorage();
        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.StoreAsync(new MemoryStream(Png(1, 1)), "..\\image.png", "image/png", default));
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
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        Assert.Throws<InvalidDataException>(() => LocalMediaStorage.EnsureSafeRoot(link));
    }

    public void Dispose()
    {
        foreach (var path in new[] { _root, _root + "-link", _root + "-target" })
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private LocalMediaStorage CreateStorage(long maxBytes = 20 * 1024 * 1024) =>
        new(Options.Create(new StorageOptions { RootPath = _root, MaxImageBytes = maxBytes }));

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        bytes[12] = 73;
        bytes[13] = 72;
        bytes[14] = 68;
        bytes[15] = 82;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
