using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.EditImage;

public sealed record ImageStageMediaItem(
    Guid Id,
    Guid OwnerContentId,
    string OriginalFileName,
    string ThumbnailUrl,
    string MimeType,
    long SizeBytes,
    int Width,
    int Height);

public sealed record ImageStageDto(
    Guid ContentId,
    WorkflowStep CurrentWorkflowStep,
    string RowVersion,
    IReadOnlyList<Guid> LinkedMediaIds,
    IReadOnlyList<ImageStageMediaItem> LinkedMedia);

public sealed record SaveImageStageRequest(
    IReadOnlyList<Guid>? MediaIds,
    bool Complete,
    string? RowVersion);

public sealed record ImageStageData(
    Content Content,
    IReadOnlyList<MediaAsset> AvailableMedia);

public interface IImageStageRepository
{
    Task<ImageStageData?> FindAsync(Guid contentId, CancellationToken cancellationToken);
    void RemoveLinks(IReadOnlyCollection<ContentMediaLink> links);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ImageStageService(IImageStageRepository repository)
{
    public async Task<ImageStageDto> GetAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var data = await FindAsync(contentId, cancellationToken);
        EnsureAvailable(data.Content.CurrentWorkflowStep);
        return Map(data);
    }

    public async Task<ImageStageDto> ExecuteAsync(
        Guid contentId,
        SaveImageStageRequest request,
        CancellationToken cancellationToken)
    {
        var data = await FindAsync(contentId, cancellationToken);
        var content = data.Content;
        if (content.CurrentWorkflowStep != WorkflowStep.MEDIA)
        {
            throw new DomainRuleException(
                IsBeforeImage(content.CurrentWorkflowStep)
                    ? "IMAGE_STAGE_NOT_AVAILABLE"
                    : "IMAGE_STAGE_ALREADY_COMPLETED",
                IsBeforeImage(content.CurrentWorkflowStep)
                    ? "분류별 편집을 완료한 뒤 이미지 단계를 진행해 주세요."
                    : "이미지 단계를 이미 완료한 콘텐츠입니다.");
        }

        if (string.IsNullOrWhiteSpace(request.RowVersion))
        {
            throw new ConcurrencyConflictException("이미지 단계 저장에는 rowversion이 필요합니다.");
        }

        ContentService.EnsureRowVersion(content, request.RowVersion);
        var requestedIds = request.MediaIds?.Distinct().ToHashSet()
            ?? throw new DomainRuleException("IMAGE_MEDIA_IDS_REQUIRED", "이미지 ID 목록이 필요합니다.");
        var availableIds = data.AvailableMedia.Select(media => media.Id).ToHashSet();
        if (!requestedIds.IsSubsetOf(availableIds))
        {
            throw new DomainRuleException(
                "IMAGE_MEDIA_NOT_AVAILABLE",
                "현재 콘텐츠에서 선택할 수 없는 이미지가 포함되어 있습니다.");
        }

        var removed = content.LinkedMedia
            .Where(link => !requestedIds.Contains(link.MediaAssetId))
            .ToArray();
        repository.RemoveLinks(removed);
        foreach (var link in removed)
        {
            content.LinkedMedia.Remove(link);
        }

        var existingIds = content.LinkedMedia.Select(link => link.MediaAssetId).ToHashSet();
        foreach (var mediaId in requestedIds.Except(existingIds))
        {
            content.LinkedMedia.Add(new ContentMediaLink
            {
                ContentId = content.Id,
                MediaAssetId = mediaId
            });
        }

        if (request.Complete)
        {
            content.MoveTo(WorkflowStep.DETAIL);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(data);
    }

    private async Task<ImageStageData> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        await repository.FindAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");

    private static void EnsureAvailable(WorkflowStep step)
    {
        if (IsBeforeImage(step))
        {
            throw new DomainRuleException(
                "IMAGE_STAGE_NOT_AVAILABLE",
                "분류별 편집을 완료한 뒤 이미지 단계를 진행해 주세요.");
        }
    }

    private static bool IsBeforeImage(WorkflowStep step) => (int)step < (int)WorkflowStep.MEDIA;

    private static ImageStageDto Map(ImageStageData data)
    {
        var linkedIds = data.Content.LinkedMedia.Select(link => link.MediaAssetId).ToHashSet();
        var linkedMedia = data.AvailableMedia
            .Where(media => linkedIds.Contains(media.Id))
            .OrderBy(media => media.SortOrder)
            .ThenBy(media => media.CreatedAtUtc)
            .Select(media => new ImageStageMediaItem(
                media.Id,
                media.ContentId,
                media.OriginalFileName,
                $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.SizeBytes,
                media.Width,
                media.Height))
            .ToArray();
        return new ImageStageDto(
            data.Content.Id,
            data.Content.CurrentWorkflowStep,
            Convert.ToBase64String(data.Content.RowVersion),
            linkedMedia.Select(media => media.Id).ToArray(),
            linkedMedia);
    }
}
