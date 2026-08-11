namespace MyDocuMgm.Application;

public sealed class CleanupService(
    ICleanupRepository repository,
    IMediaCleanupStorage storage,
    IMediaDiagnostics diagnostics)
{
    public Task<IReadOnlyList<TrashContentItem>> ListTrashAsync(CancellationToken cancellationToken) =>
        repository.ListTrashAsync(cancellationToken);

    public async Task<IReadOnlyList<OrphanMediaItem>> ListOrphanMediaAsync(CancellationToken cancellationToken)
    {
        var candidates = await repository.ListOrphanMediaAsync(cancellationToken);
        var result = new List<OrphanMediaItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var media = candidate.Media;
            var state = await storage.InspectAsync(
                media.ContentId,
                media.Id,
                media.RelativePath,
                media.StoredFileName,
                cancellationToken);
            result.Add(new OrphanMediaItem(
                media.Id,
                media.ContentId,
                media.OriginalFileName,
                candidate.LinkCount,
                state.Exists,
                state.Code,
                Convert.ToBase64String(media.RowVersion)));
        }

        return result;
    }

    public async Task PermanentlyDeleteContentAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await repository.FindContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        if (!content.IsDeleted)
        {
            throw new CleanupConflictException(
                "CONTENT_NOT_SOFT_DELETED",
                "휴지통으로 이동한 콘텐츠만 영구 삭제할 수 있습니다.");
        }

        if (await repository.CountOwnedMediaAsync(contentId, cancellationToken) != 0)
        {
            throw new CleanupConflictException(
                "CONTENT_HAS_OWNED_MEDIA",
                "소유 미디어를 먼저 영구 삭제해야 콘텐츠를 영구 삭제할 수 있습니다.");
        }

        await repository.RemoveSourceEvidenceAsync(contentId, cancellationToken);
        repository.RemoveContent(content);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task PermanentlyDeleteOrphanMediaAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var candidate = await RequireDeletableMediaAsync(mediaId, cancellationToken);
        var media = candidate.Media;
        if (await repository.CountMediaPathReferencesAsync(media.RelativePath, media.Id, cancellationToken) != 0)
        {
            throw new CleanupConflictException(
                "MEDIA_PATH_SHARED",
                "동일한 물리 파일을 참조하는 다른 미디어가 있어 삭제할 수 없습니다.");
        }

        var cleanup = await storage.PrepareDeleteAsync(
            media.ContentId,
            media.Id,
            media.RelativePath,
            media.StoredFileName,
            cancellationToken);

        try
        {
            candidate = await RequireDeletableMediaAsync(mediaId, cancellationToken);
            if (await repository.CountMediaPathReferencesAsync(media.RelativePath, media.Id, cancellationToken) != 0)
            {
                throw new CleanupConflictException(
                    "MEDIA_PATH_SHARED",
                    "삭제 직전에 파일 공유 참조가 확인되어 작업을 중단했습니다.");
            }

            repository.RemoveMedia(candidate.Media);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception databaseFailure)
        {
            try
            {
                await storage.RestoreAsync(cleanup, CancellationToken.None);
            }
            catch
            {
                diagnostics.Record("MEDIA_CLEANUP_RESTORE_FAILED", media.ContentId, media.Id);
                throw new MediaOperationException(
                    "MEDIA_CLEANUP_RESTORE_FAILED",
                    $"DB 삭제 실패 후 격리 파일 복원에도 실패했습니다: {databaseFailure.GetType().Name}");
            }
            throw;
        }

        try
        {
            await storage.CommitAsync(cleanup, CancellationToken.None);
        }
        catch
        {
            diagnostics.Record("MEDIA_CLEANUP_FINALIZE_FAILED", media.ContentId, media.Id);
            throw new MediaOperationException(
                "MEDIA_CLEANUP_FINALIZE_FAILED",
                "미디어 레코드는 삭제되었지만 격리 파일의 최종 정리에 실패했습니다.");
        }
    }

    private async Task<CleanupMediaCandidate> RequireDeletableMediaAsync(
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var candidate = await repository.FindMediaAsync(mediaId, cancellationToken)
            ?? throw new NotFoundException("미디어를 찾을 수 없습니다.");
        if (candidate.LinkCount != 0 || candidate.StepReferenceCount != 0)
        {
            throw new CleanupConflictException(
                "MEDIA_STILL_REFERENCED",
                "콘텐츠 또는 단계에서 참조 중인 미디어는 영구 삭제할 수 없습니다.");
        }

        return candidate;
    }
}
