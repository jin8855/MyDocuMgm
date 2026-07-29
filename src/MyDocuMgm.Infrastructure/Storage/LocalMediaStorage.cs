using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application;

namespace MyDocuMgm.Infrastructure.Storage;

public sealed class LocalMediaStorage(IOptions<StorageOptions> options) : IMediaStorage
{
    private readonly StorageOptions _options = options.Value;

    public async Task<StoredMedia> StoreAsync(
        Stream source,
        string originalFileName,
        string declaredMimeType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException("Storage:RootPath가 설정되지 않았습니다.");
        }

        if (Path.IsPathRooted(originalFileName) || Path.GetFileName(originalFileName) != originalFileName)
        {
            throw new InvalidDataException("원본 파일명에 경로를 포함할 수 없습니다.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > _options.MaxImageBytes)
            {
                throw new InvalidDataException("이미지는 최대 20MB까지 등록할 수 있습니다.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        var bytes = buffer.ToArray();
        var format = ImageSignature.Read(bytes);
        if (!string.Equals(format.MimeType, declaredMimeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("선언한 MIME 형식과 실제 이미지 서명이 일치하지 않습니다.");
        }

        var originalExtension = Path.GetExtension(originalFileName);
        if (!format.AllowedExtensions.Contains(originalExtension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("원본 확장자와 실제 이미지 서명이 일치하지 않습니다.");
        }

        var root = EnsureSafeRoot(_options.RootPath);
        var relativeDirectory = Path.Combine(DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var storedFileName = $"{Guid.NewGuid():N}{format.Extension}";
        var relativePath = Path.Combine(relativeDirectory, storedFileName);
        var fullPath = ResolveUnderRoot(root, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        RejectReparsePoints(root, directory);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        return new StoredMedia(
            storedFileName,
            relativePath.Replace(Path.DirectorySeparatorChar, '/'),
            format.MimeType,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)),
            format.Width,
            format.Height);
    }

    public Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = EnsureSafeRoot(_options.RootPath);
        var fullPath = ResolveUnderRoot(root, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    internal static string EnsureSafeRoot(string configuredRoot)
    {
        if (!Path.IsPathFullyQualified(configuredRoot))
        {
            throw new InvalidOperationException("자료 루트는 절대경로여야 합니다.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        if (Directory.Exists(root))
        {
            RejectReparsePoints(root, root);
        }

        return root;
    }

    internal static string ResolveUnderRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("절대경로 입력은 허용되지 않습니다.");
        }

        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("자료 루트 밖의 경로는 허용되지 않습니다.");
        }

        return candidate;
    }

    private static void RejectReparsePoints(string root, string targetDirectory)
    {
        var current = new DirectoryInfo(targetDirectory);
        var rootFullName = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        while (current is not null && current.FullName.StartsWith(rootFullName, StringComparison.OrdinalIgnoreCase))
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("재분석 지점 또는 심볼릭 링크를 통한 자료 루트 이탈은 허용되지 않습니다.");
            }

            if (string.Equals(Path.TrimEndingDirectorySeparator(current.FullName), rootFullName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = current.Parent;
        }
    }

    private sealed record ImageFormat(
        string MimeType,
        string Extension,
        IReadOnlyList<string> AllowedExtensions,
        int Width,
        int Height);

    private static class ImageSignature
    {
        public static ImageFormat Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length >= 24 &&
                bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10)
            {
                return new(
                    "image/png",
                    ".png",
                    [".png"],
                    BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]),
                    BinaryPrimitives.ReadInt32BigEndian(bytes[20..24]));
            }

            if (bytes.Length >= 30 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            {
                return ReadWebP(bytes);
            }

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return ReadJpeg(bytes);
            }

            throw new InvalidDataException("JPG, PNG 또는 WebP 이미지 서명을 찾을 수 없습니다.");
        }

        private static ImageFormat ReadWebP(ReadOnlySpan<byte> bytes)
        {
            if (bytes[12..16].SequenceEqual("VP8X"u8) && bytes.Length >= 30)
            {
                var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                return new("image/webp", ".webp", [".webp"], width, height);
            }

            if (bytes[12..16].SequenceEqual("VP8 "u8) && bytes.Length >= 30 &&
                bytes[23] == 0x9D && bytes[24] == 0x01 && bytes[25] == 0x2A)
            {
                var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3FFF;
                var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3FFF;
                return new("image/webp", ".webp", [".webp"], width, height);
            }

            if (bytes[12..16].SequenceEqual("VP8L"u8) && bytes.Length >= 25 && bytes[20] == 0x2F)
            {
                var width = 1 + bytes[21] + ((bytes[22] & 0x3F) << 8);
                var height = 1 + (bytes[22] >> 6) + (bytes[23] << 2) + ((bytes[24] & 0x0F) << 10);
                return new("image/webp", ".webp", [".webp"], width, height);
            }

            throw new InvalidDataException("지원되는 WebP 헤더(VP8X)가 아닙니다.");
        }

        private static ImageFormat ReadJpeg(ReadOnlySpan<byte> bytes)
        {
            var index = 2;
            while (index + 8 < bytes.Length)
            {
                if (bytes[index] != 0xFF)
                {
                    index++;
                    continue;
                }

                var marker = bytes[index + 1];
                if (marker is 0xC0 or 0xC1 or 0xC2)
                {
                    var height = BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 5)..(index + 7)]);
                    var width = BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 7)..(index + 9)]);
                    return new("image/jpeg", ".jpg", [".jpg", ".jpeg"], width, height);
                }

                if (index + 4 > bytes.Length)
                {
                    break;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 2)..(index + 4)]);
                if (length < 2)
                {
                    break;
                }

                index += 2 + length;
            }

            throw new InvalidDataException("JPEG 크기 정보를 읽을 수 없습니다.");
        }
    }
}
