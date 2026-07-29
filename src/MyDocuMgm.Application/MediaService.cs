using MyDocuMgm.Domain;

namespace MyDocuMgm.Application;

public sealed class MediaService(
    IContentRepository contentRepository,
    IMediaAssetRepository mediaRepository,
    IMediaStorage storage)
{
    public Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken) =>
        mediaRepository.ListAsync(contentId, includeDeleted, cancellationToken);

    public async Task<MediaAsset> UploadAsync(
        Guid contentId,
        Stream source,
        string originalFileName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        _ = await contentRepository.FindAsync(contentId, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");

        var media = new MediaAsset
        {
            ContentId = contentId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = $"{Guid.NewGuid():N}.pending",
            RelativePath = $"pending/{Guid.NewGuid():N}",
            MimeType = "application/octet-stream",
            Sha256 = new string('0', 64),
            StorageStatus = MediaStorageStatus.PENDING
        };
        await mediaRepository.AddAsync(media, cancellationToken);
        await mediaRepository.SaveChangesAsync(cancellationToken);

        StoredMedia? stored = null;
        try
        {
            stored = await storage.StoreAsync(source, originalFileName, mimeType, cancellationToken);
            media.StoredFileName = stored.StoredFileName;
            media.RelativePath = stored.RelativePath;
            media.MimeType = stored.MimeType;
            media.SizeBytes = stored.SizeBytes;
            media.Sha256 = stored.Sha256;
            media.Width = stored.Width;
            media.Height = stored.Height;
            media.MarkReady();
            await mediaRepository.SaveChangesAsync(cancellationToken);
            return media;
        }
        catch (Exception exception)
        {
            if (stored is not null)
            {
                await storage.DeleteIfExistsAsync(stored.RelativePath, CancellationToken.None);
            }

            media.MarkFailed(exception.Message[..Math.Min(exception.Message.Length, 500)]);
            await mediaRepository.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(Guid contentId, Guid mediaId, CancellationToken cancellationToken)
    {
        var media = await mediaRepository.FindAsync(contentId, mediaId, false, cancellationToken)
            ?? throw new NotFoundException("이미지를 찾을 수 없습니다.");
        media.SoftDelete();
        await mediaRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<MediaAsset> UpdateAsync(
        Guid contentId,
        Guid mediaId,
        string? description,
        bool isPublicAllowed,
        CancellationToken cancellationToken)
    {
        var media = await mediaRepository.FindAsync(contentId, mediaId, false, cancellationToken)
            ?? throw new NotFoundException("이미지를 찾을 수 없습니다.");
        media.Description = description?.Trim();
        media.IsPublicAllowed = isPublicAllowed;
        await mediaRepository.SaveChangesAsync(cancellationToken);
        return media;
    }

    public async Task ReorderAsync(Guid contentId, IReadOnlyList<Guid> mediaIds, CancellationToken cancellationToken)
    {
        var media = await mediaRepository.ListAsync(contentId, false, cancellationToken);
        if (media.Count != mediaIds.Count || mediaIds.Distinct().Count() != mediaIds.Count ||
            media.Any(item => !mediaIds.Contains(item.Id)))
        {
            throw new DomainRuleException("INVALID_MEDIA_ORDER", "정렬 목록은 현재 이미지 전체를 중복 없이 포함해야 합니다.");
        }

        for (var index = 0; index < mediaIds.Count; index++)
        {
            media.Single(item => item.Id == mediaIds[index]).SortOrder = index + 1;
        }

        await mediaRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid contentId, Guid mediaId, CancellationToken cancellationToken)
    {
        var media = await mediaRepository.FindAsync(contentId, mediaId, true, cancellationToken)
            ?? throw new NotFoundException("이미지를 찾을 수 없습니다.");
        media.Restore();
        await mediaRepository.SaveChangesAsync(cancellationToken);
    }
}
