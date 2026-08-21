using MyDocuMgm.Domain;

namespace MyDocuMgm.Application;

public enum SearchScope
{
    ALL,
    TAG
}

public sealed record ContentQuery(
    string? Keyword,
    string? MajorCategory,
    string? AttributeKey,
    string? AttributeValue,
    SearchScope SearchScope,
    Guid? CategoryId,
    ContentStatus? Status,
    WorkflowStep? WorkflowStep,
    bool? IsFavorite,
    bool IncludeDeleted = false,
    int Page = 1,
    int PageSize = 30);

public sealed record ContentSummary(
    Guid Id,
    string CategoryCode,
    string CategoryDisplayName,
    string Title,
    string? ShortSummary,
    ContentStatus Status,
    ContentVisibility Visibility,
    bool IsFavorite,
    WorkflowStep CurrentWorkflowStep,
    string BlogDraftStatus,
    DateTime UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<string> Tags);

public sealed record ContentDetail(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string Title,
    string? ShortSummary,
    string? DetailContent,
    ContentStatus Status,
    ContentVisibility Visibility,
    bool IsFavorite,
    ExperienceStatus ExperienceStatus,
    WorkflowStep CurrentWorkflowStep,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<string> Tags);

public sealed record SaveContentRequest(
    Guid CategoryId,
    string Title,
    string? ShortSummary,
    string? DetailContent,
    ContentStatus Status,
    ContentVisibility Visibility,
    bool IsFavorite,
    ExperienceStatus ExperienceStatus,
    IReadOnlyList<string>? Tags,
    string? RowVersion);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface IContentTagRepository
{
    Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken);
    Task AddTagAsync(Tag tag, CancellationToken cancellationToken);
}

public interface IContentRepository : IContentTagRepository
{
    Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken);
    Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken);
    Task AddAsync(Content content, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IMediaAssetRepository
{
    Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken);
    Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken);
    Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken);
    Task<MediaAsset?> FindReadyDuplicateAsync(
        Guid contentId,
        string sha256,
        long sizeBytes,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MediaAsset>> ListAllAsync(bool includeDeleted, CancellationToken cancellationToken);
    Task AddAsync(MediaAsset media, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public enum MediaFilter
{
    ALL,
    SELECTED,
    DUPLICATE,
    DELETED
}

public enum MediaSort
{
    TIME_ASC,
    TIME_DESC
}

public sealed record MediaQuery(
    Guid ContentId,
    MediaFilter Filter = MediaFilter.ALL,
    MediaSort Sort = MediaSort.TIME_ASC,
    int Page = 1,
    int PageSize = 24);

public sealed record MediaItemDto(
    Guid Id,
    string OriginalFileName,
    string ThumbnailUrl,
    string MimeType,
    long SizeBytes,
    int Width,
    int Height,
    int SortOrder,
    long? SourceTimestampMs,
    bool IsSelected,
    bool IsPublicAllowed,
    string? Description,
    MediaStorageStatus StorageStatus,
    string Sha256,
    bool IsDeleted,
    DateTime? DeletedAtUtc,
    string RowVersion);

public sealed record MediaPage(
    IReadOnlyList<MediaItemDto> Items,
    int TotalCount,
    int SelectedCount,
    int DuplicateCount,
    int DeletedCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record UpdateMediaMetadataRequest(
    string? Description,
    bool IsPublicAllowed,
    bool IsSelected,
    long? SourceTimestampMs,
    string RowVersion);

public sealed record MediaUploadResult(MediaItemDto Item, bool Reused);

public sealed record PreparedMedia(
    string TemporaryRelativePath,
    string StoredFileName,
    string RelativePath,
    string MimeType,
    long SizeBytes,
    string Sha256,
    int Width,
    int Height);

public sealed record MediaBinary(Stream Content, string MimeType);

public sealed record MediaIntegrityResult(bool IsValid, string Code);

public sealed record MediaStorageReference(
    Guid MediaId,
    string RelativePath,
    string MimeType,
    long SizeBytes,
    string Sha256);

public sealed record MediaReconciliationIssue(string Code, Guid? MediaId);

public sealed record MediaReconciliationReport(
    int ReferencedCount,
    int CheckedCount,
    int MissingCount,
    int IntegrityMismatchCount,
    int OrphanCount,
    int StaleTemporaryCount,
    IReadOnlyList<MediaReconciliationIssue> Issues);

public interface IMediaStorage
{
    Task<PreparedMedia> PrepareAsync(
        Stream source,
        Guid contentId,
        Guid mediaId,
        string originalFileName,
        string declaredMimeType,
        CancellationToken cancellationToken);
    Task PromoteAsync(PreparedMedia prepared, CancellationToken cancellationToken);
    Task DiscardPreparedAsync(PreparedMedia prepared, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken);
    Task<MediaBinary> OpenOriginalAsync(
        string relativePath,
        string mimeType,
        CancellationToken cancellationToken);
    Task<MediaBinary> GetOrCreateThumbnailAsync(
        Guid mediaId,
        string relativePath,
        string expectedMimeType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken);
    Task<MediaIntegrityResult> VerifyAsync(
        string relativePath,
        string expectedMimeType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken);
    Task<MediaReconciliationReport> ReconcileAsync(
        IReadOnlyList<MediaStorageReference> references,
        CancellationToken cancellationToken);
}

public interface IMediaDiagnostics
{
    void Record(string code, Guid? contentId, Guid? mediaId = null);
}

public sealed record MediaStorageReadiness(bool IsReady, string Code);

public interface IMediaStorageReadiness
{
    Task<MediaStorageReadiness> CheckReadinessAsync(CancellationToken cancellationToken);
}

public sealed record TrashContentItem(
    Guid Id,
    string Title,
    DateTime? DeletedAtUtc,
    int OwnedMediaCount,
    string RowVersion);

public sealed record OrphanMediaItem(
    Guid Id,
    Guid ContentId,
    string OriginalFileName,
    int LinkCount,
    bool FileExists,
    string FileState,
    string RowVersion);

public sealed record CleanupMediaCandidate(
    MediaAsset Media,
    int LinkCount,
    int StepReferenceCount);

public interface ICleanupRepository
{
    Task<IReadOnlyList<TrashContentItem>> ListTrashAsync(CancellationToken cancellationToken);
    Task<Content?> FindContentAsync(Guid id, CancellationToken cancellationToken);
    Task<int> CountOwnedMediaAsync(Guid contentId, CancellationToken cancellationToken);
    Task RemoveSourceEvidenceAsync(Guid contentId, CancellationToken cancellationToken);
    void RemoveContent(Content content);
    Task<IReadOnlyList<CleanupMediaCandidate>> ListOrphanMediaAsync(CancellationToken cancellationToken);
    Task<CleanupMediaCandidate?> FindMediaAsync(Guid mediaId, CancellationToken cancellationToken);
    Task<int> CountMediaPathReferencesAsync(string relativePath, Guid excludingMediaId, CancellationToken cancellationToken);
    void RemoveMedia(MediaAsset media);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record MediaCleanupStorageState(bool Exists, bool IsSafe, string Code);
public sealed record QuarantinedMediaFile(string OriginalRelativePath, string QuarantineRelativePath);
public sealed record PreparedMediaCleanup(IReadOnlyList<QuarantinedMediaFile> Files, bool OriginalAlreadyAbsent);

public interface IMediaCleanupStorage
{
    Task<MediaCleanupStorageState> InspectAsync(
        Guid contentId,
        Guid mediaId,
        string relativePath,
        string storedFileName,
        CancellationToken cancellationToken);
    Task<PreparedMediaCleanup> PrepareDeleteAsync(
        Guid contentId,
        Guid mediaId,
        string relativePath,
        string storedFileName,
        CancellationToken cancellationToken);
    Task RestoreAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken);
    Task CommitAsync(PreparedMediaCleanup cleanup, CancellationToken cancellationToken);
}

public sealed class MediaOperationException : Exception
{
    public MediaOperationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ConcurrencyConflictException(string message) : InvalidOperationException(message);
public sealed class NotFoundException(string message) : InvalidOperationException(message);

public sealed class CleanupConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
