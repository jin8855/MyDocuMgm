using MyDocuMgm.Domain;

namespace MyDocuMgm.Application;

public sealed record ContentQuery(
    string? Search,
    Guid? CategoryId,
    ContentStatus? Status,
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
    DateTime UpdatedAtUtc,
    string RowVersion);

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

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public interface IContentRepository
{
    Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken);
    Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken);
    Task AddAsync(Content content, CancellationToken cancellationToken);
    Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IMediaAssetRepository
{
    Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken);
    Task<IReadOnlyList<MediaAsset>> ListAsync(Guid contentId, bool includeDeleted, CancellationToken cancellationToken);
    Task AddAsync(MediaAsset media, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record StoredMedia(
    string StoredFileName,
    string RelativePath,
    string MimeType,
    long SizeBytes,
    string Sha256,
    int Width,
    int Height);

public interface IMediaStorage
{
    Task<StoredMedia> StoreAsync(Stream source, string originalFileName, string declaredMimeType, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string relativePath, CancellationToken cancellationToken);
}

public sealed class ConcurrencyConflictException(string message) : InvalidOperationException(message);
public sealed class NotFoundException(string message) : InvalidOperationException(message);
