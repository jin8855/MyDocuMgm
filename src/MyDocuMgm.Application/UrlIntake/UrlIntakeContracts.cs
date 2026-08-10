using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.UrlIntake;

public sealed record CreateUrlIntakeRequest(string Url);

public sealed record CreateInstagramIntakeRequest(string Url);

public sealed record SaveManualBodyRequest(string Body);

public sealed record SaveManualInstagramRequest(
    string Caption,
    string PinnedAuthorCommentState,
    string? PinnedAuthorCommentText,
    IReadOnlyList<Guid> MediaIds);

public sealed record ReplaceLinkedMediaRequest(IReadOnlyList<Guid> MediaIds);

public sealed record UrlIntakeDto(
    Guid Id,
    string OriginalUrl,
    string NormalizedUrl,
    string SourceKind,
    string Status,
    bool IsDuplicate,
    string? ManualBody,
    bool ManualBodyPresent,
    string? InstagramContentType,
    string? ManualCaption,
    string? PinnedAuthorCommentState,
    string? PinnedAuthorCommentText,
    string? SourceAcquisitionMode,
    IReadOnlyList<Guid> LinkedMediaIds);

public sealed record LinkableMediaItem(
    Guid Id,
    Guid OwnerContentId,
    string OriginalFileName,
    string ThumbnailUrl,
    string MimeType,
    long SizeBytes,
    int Width,
    int Height);

public sealed record LinkableMediaPage(
    IReadOnlyList<LinkableMediaItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record UrlIntakeStoreResult(Content Content, bool Created);

public interface IUrlIntakeRepository
{
    Task<Content?> FindByNormalizedUrlAsync(string normalizedUrl, CancellationToken cancellationToken);
    Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken);
    Task<UrlIntakeStoreResult> AddOrGetAsync(Content content, CancellationToken cancellationToken);
    Task<LinkableMediaPage> ListLinkableMediaAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task ReplaceLinkedMediaAsync(
        Content content,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken);
    Task SaveManualInstagramAsync(
        Content content,
        string caption,
        PinnedAuthorCommentState commentState,
        string? commentText,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
