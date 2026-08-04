using System.Collections.Concurrent;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application;

public sealed class MediaService(
    IContentRepository contentRepository,
    IMediaAssetRepository mediaRepository,
    IMediaStorage storage,
    IMediaDiagnostics? diagnostics = null)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UploadGates = new();

    public Task<IReadOnlyList<MediaAsset>> ListAsync(
        Guid contentId,
        bool includeDeleted,
        CancellationToken cancellationToken) =>
        mediaRepository.ListAsync(contentId, includeDeleted, cancellationToken);

    public Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is not (24 or 48 or 96))
        {
            throw new DomainRuleException(
                "INVALID_MEDIA_PAGE",
                "이미지 페이지는 1 이상이고 크기는 24, 48, 96 중 하나여야 합니다.");
        }

        return mediaRepository.SearchAsync(query, cancellationToken);
    }

    public async Task<MediaUploadResult> UploadAsync(
        Guid contentId,
        Stream source,
        string originalFileName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        _ = await contentRepository.FindAsync(contentId, false, cancellationToken)
            ?? throw Error("MEDIA_CONTENT_NOT_FOUND", "콘텐츠를 찾을 수 없습니다.");

        var mediaId = Guid.NewGuid();
        var prepared = await storage.PrepareAsync(
            source,
            contentId,
            mediaId,
            originalFileName,
            mimeType,
            cancellationToken);

        var uploadGate = UploadGates.GetOrAdd(contentId, static _ => new SemaphoreSlim(1, 1));
        try
        {
            await uploadGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await DiscardPreparedWithDiagnosticAsync(prepared, contentId, mediaId);
            throw new MediaOperationException(
                "MEDIA_OPERATION_CANCELLED",
                "이미지 작업이 취소되었습니다.");
        }

        try
        {
        var duplicate = await mediaRepository.FindReadyDuplicateAsync(
            contentId,
            prepared.Sha256,
            prepared.SizeBytes,
            cancellationToken);
        if (duplicate is not null)
        {
            await DiscardPreparedWithDiagnosticAsync(prepared, contentId, mediaId);
            return new(ToDto(duplicate), true);
        }

        var existing = await mediaRepository.ListAsync(contentId, false, cancellationToken);
        var media = new MediaAsset
        {
            Id = mediaId,
            ContentId = contentId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = prepared.StoredFileName,
            RelativePath = prepared.RelativePath,
            MimeType = prepared.MimeType,
            SizeBytes = prepared.SizeBytes,
            Sha256 = prepared.Sha256,
            Width = prepared.Width,
            Height = prepared.Height,
            SortOrder = existing.Count == 0 ? 1 : existing.Max(item => item.SortOrder) + 1,
            StorageStatus = MediaStorageStatus.PENDING
        };

        try
        {
            await mediaRepository.AddAsync(media, cancellationToken);
            await mediaRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await DiscardPreparedWithDiagnosticAsync(prepared, contentId, mediaId);
            throw WrapPersistenceFailure(exception);
        }

        try
        {
            await storage.PromoteAsync(prepared, cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkFailedWithDiagnosticAsync(media, FailureCode(exception), contentId);
            await DiscardPreparedWithDiagnosticAsync(prepared, contentId, mediaId);
            throw;
        }

        media.MarkReady();
        try
        {
            await mediaRepository.SaveChangesAsync(cancellationToken);
            return new(ToDto(media), false);
        }
        catch (Exception exception)
        {
            try
            {
                await storage.DeleteIfExistsAsync(prepared.RelativePath, CancellationToken.None);
            }
            catch
            {
                diagnostics?.Record("MEDIA_COMPENSATION_DELETE_FAILED", contentId, mediaId);
            }

            await MarkFailedWithDiagnosticAsync(media, "MEDIA_READY_PERSIST_FAILED", contentId);
            throw WrapPersistenceFailure(exception);
        }
        }
        finally
        {
            uploadGate.Release();
        }
    }

    public async Task<MediaBinary> OpenOriginalAsync(
        Guid contentId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var media = await FindReadyAsync(contentId, mediaId, false, cancellationToken);
        await EnsureIntegrityAsync(media, cancellationToken);
        return await storage.OpenOriginalAsync(media.RelativePath, media.MimeType, cancellationToken);
    }

    public async Task<MediaBinary> OpenThumbnailAsync(
        Guid contentId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var media = await FindReadyAsync(contentId, mediaId, false, cancellationToken);
        return await storage.GetOrCreateThumbnailAsync(
            media.Id,
            media.RelativePath,
            media.MimeType,
            media.SizeBytes,
            media.Sha256,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid contentId,
        Guid mediaId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var media = await FindReadyAsync(contentId, mediaId, false, cancellationToken);
        EnsureRowVersion(media, rowVersion);
        media.SoftDelete();
        await SaveMediaChangesAsync(cancellationToken);
    }

    public async Task<MediaItemDto> UpdateAsync(
        Guid contentId,
        Guid mediaId,
        UpdateMediaMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var media = await FindReadyAsync(contentId, mediaId, false, cancellationToken);
        EnsureRowVersion(media, request.RowVersion);
        media.Description = request.Description?.Trim();
        media.IsPublicAllowed = request.IsPublicAllowed;
        media.IsSelected = request.IsSelected;
        media.SourceTimestampMs = request.SourceTimestampMs;
        await SaveMediaChangesAsync(cancellationToken);
        return ToDto(media);
    }

    public async Task ReorderAsync(
        Guid contentId,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var media = await mediaRepository.ListAsync(contentId, false, cancellationToken);
        if (media.Count != mediaIds.Count ||
            mediaIds.Distinct().Count() != mediaIds.Count ||
            media.Any(item => !mediaIds.Contains(item.Id)))
        {
            throw new DomainRuleException(
                "INVALID_MEDIA_ORDER",
                "정렬 목록은 현재 READY 이미지 전체를 중복 없이 포함해야 합니다.");
        }

        for (var index = 0; index < mediaIds.Count; index++)
        {
            media.Single(item => item.Id == mediaIds[index]).SortOrder = index + 1;
        }

        await SaveMediaChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(
        Guid contentId,
        Guid mediaId,
        int direction,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        if (direction is not (-1 or 1))
        {
            throw Error("MEDIA_MOVE_DIRECTION_INVALID", "이미지 이동 방향이 올바르지 않습니다.");
        }

        var media = await mediaRepository.ListAsync(contentId, false, cancellationToken);
        var ordered = media
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedAtUtc)
            .ToList();
        var currentIndex = ordered.FindIndex(item => item.Id == mediaId);
        if (currentIndex < 0)
        {
            throw Error("MEDIA_NOT_FOUND", "이미지를 찾을 수 없습니다.");
        }

        EnsureRowVersion(ordered[currentIndex], rowVersion);
        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        (ordered[currentIndex].SortOrder, ordered[targetIndex].SortOrder) =
            (ordered[targetIndex].SortOrder, ordered[currentIndex].SortOrder);
        await SaveMediaChangesAsync(cancellationToken);
    }

    public async Task<MediaItemDto> RestoreAsync(
        Guid contentId,
        Guid mediaId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var media = await FindReadyAsync(contentId, mediaId, true, cancellationToken);
        if (!media.IsDeleted)
        {
            throw Error("MEDIA_NOT_DELETED", "삭제되지 않은 이미지는 복원할 수 없습니다.");
        }

        EnsureRowVersion(media, rowVersion);
        var integrity = await storage.VerifyAsync(
            media.RelativePath,
            media.MimeType,
            media.SizeBytes,
            media.Sha256,
            cancellationToken);
        if (!integrity.IsValid)
        {
            diagnostics?.Record(integrity.Code, contentId, mediaId);
            throw Error(
                "MEDIA_RESTORE_BLOCKED",
                "원본 이미지 무결성을 확인할 수 없어 복원하지 않았습니다.");
        }

        media.Restore();
        await SaveMediaChangesAsync(cancellationToken);
        return ToDto(media);
    }

    public async Task<MediaReconciliationReport> ReconcileAsync(CancellationToken cancellationToken)
    {
        var media = await mediaRepository.ListAllAsync(includeDeleted: true, cancellationToken);
        var references = media
            .Select(item => new MediaStorageReference(
                item.Id,
                item.RelativePath,
                item.MimeType,
                item.SizeBytes,
                item.Sha256))
            .ToList();
        return await storage.ReconcileAsync(references, cancellationToken);
    }

    private async Task<MediaAsset> FindReadyAsync(
        Guid contentId,
        Guid mediaId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var media = await mediaRepository.FindAsync(contentId, mediaId, includeDeleted, cancellationToken);
        if (media is null || media.StorageStatus != MediaStorageStatus.READY)
        {
            throw Error("MEDIA_NOT_FOUND", "이미지를 찾을 수 없습니다.");
        }

        return media;
    }

    private async Task EnsureIntegrityAsync(MediaAsset media, CancellationToken cancellationToken)
    {
        var integrity = await storage.VerifyAsync(
            media.RelativePath,
            media.MimeType,
            media.SizeBytes,
            media.Sha256,
            cancellationToken);
        if (!integrity.IsValid)
        {
            diagnostics?.Record(integrity.Code, media.ContentId, media.Id);
            throw Error(integrity.Code, "이미지 원본 무결성 검증에 실패했습니다.");
        }
    }

    private async Task SaveMediaChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await mediaRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new MediaOperationException(
                "MEDIA_CONCURRENCY_CONFLICT",
                "다른 변경이 먼저 저장되어 이미지 작업을 완료하지 못했습니다.");
        }
    }

    private async Task DiscardPreparedWithDiagnosticAsync(
        PreparedMedia prepared,
        Guid contentId,
        Guid mediaId)
    {
        try
        {
            await storage.DiscardPreparedAsync(prepared, CancellationToken.None);
        }
        catch
        {
            diagnostics?.Record("MEDIA_COMPENSATION_TEMP_DELETE_FAILED", contentId, mediaId);
        }
    }

    private async Task MarkFailedWithDiagnosticAsync(
        MediaAsset media,
        string code,
        Guid contentId)
    {
        media.MarkFailed(code);
        try
        {
            await mediaRepository.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            diagnostics?.Record("MEDIA_FAILED_STATE_PERSIST_FAILED", contentId, media.Id);
        }
    }

    private static MediaOperationException WrapPersistenceFailure(Exception exception) =>
        exception is MediaOperationException media
            ? media
            : new MediaOperationException(
                exception is ConcurrencyConflictException
                    ? "MEDIA_CONCURRENCY_CONFLICT"
                    : "MEDIA_PERSISTENCE_FAILED",
                "이미지 상태를 저장하지 못했습니다.");

    private static string FailureCode(Exception exception) =>
        exception is MediaOperationException media ? media.Code : "MEDIA_STORAGE_FAILED";

    private static void EnsureRowVersion(MediaAsset media, string rowVersion) =>
        ContentService.EnsureRowVersion(new Content { RowVersion = media.RowVersion }, rowVersion);

    private static MediaItemDto ToDto(MediaAsset media) =>
        new(
            media.Id,
            media.OriginalFileName,
            $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
            media.MimeType,
            media.SizeBytes,
            media.Width,
            media.Height,
            media.SortOrder,
            media.SourceTimestampMs,
            media.IsSelected,
            media.IsPublicAllowed,
            media.Description,
            media.StorageStatus,
            media.Sha256,
            media.IsDeleted,
            media.DeletedAtUtc,
            Convert.ToBase64String(media.RowVersion));

    private static MediaOperationException Error(string code, string message) =>
        new(code, message);
}
