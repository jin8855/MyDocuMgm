using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.ExternalFetch;

public enum ExternalFetchFailureKind
{
    VALIDATION,
    POLICY,
    REMOTE,
    TIMEOUT,
    RETRY_LIMIT
}

public sealed class ExternalFetchException(
    string code,
    string message,
    ExternalFetchFailureKind kind,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public ExternalFetchFailureKind Kind { get; } = kind;
}

public sealed record ExternalPageResponse(
    string FinalUrl,
    int HttpStatusCode,
    string ResponseMimeType,
    long ResponseBytes,
    string ContentSha256,
    string? ETag,
    DateTime? LastModifiedAtUtc,
    string Html);

public sealed record ExtractedPageContent(
    string? Title,
    string? Description,
    string? AuthorName,
    DateTime? PublishedAtUtc,
    string Body);

public interface IExternalPageFetcher
{
    Task<ExternalPageResponse> FetchAsync(Uri uri, CancellationToken cancellationToken);
}

public interface IHtmlContentExtractor
{
    ExtractedPageContent Extract(string html, Uri finalUri);
}

public interface IExternalFetchRepository
{
    Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken);
    Task<ExternalFetchAttempt> CreateAttemptAsync(
        Content content,
        DateTime recentCutoffUtc,
        CancellationToken cancellationToken);
    Task<ExternalFetchAttempt?> FindAttemptAsync(
        Guid contentId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<ExternalFetchAttempt?> FindLatestAttemptAsync(
        Guid contentId,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task ApplyAsync(
        Content content,
        ExternalFetchAttempt attempt,
        SourceEvidence evidence,
        CancellationToken cancellationToken);
}

public sealed record ApplyExternalFetchRequest(
    string? Title,
    string? Description,
    string Body);

public sealed record ExternalFetchAttemptDto(
    Guid Id,
    Guid ContentId,
    int AttemptNumber,
    string Status,
    string? FinalUrl,
    int? HttpStatusCode,
    string? ResponseMimeType,
    long? ResponseBytes,
    string? ContentSha256,
    string? ETag,
    DateTime? LastModifiedAtUtc,
    string? Title,
    string? Description,
    string? AuthorName,
    DateTime? PublishedAtUtc,
    string? Body,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record ExternalFetchApplyDto(
    ExternalFetchAttemptDto Attempt,
    UrlIntakeDto Intake);

public static class ExternalFetchMappings
{
    public static ExternalFetchAttemptDto ToDto(ExternalFetchAttempt attempt) => new(
        attempt.Id,
        attempt.ContentId,
        attempt.AttemptNumber,
        attempt.Status.ToString(),
        attempt.FinalUrl,
        attempt.HttpStatusCode,
        attempt.ResponseMimeType,
        attempt.ResponseBytes,
        attempt.ContentSha256,
        attempt.ETag,
        attempt.LastModifiedAtUtc,
        attempt.PageTitle,
        attempt.PageDescription,
        attempt.AuthorName,
        attempt.PublishedAtUtc,
        attempt.ExtractedText,
        attempt.ErrorCode,
        attempt.ErrorMessage,
        attempt.StartedAtUtc,
        attempt.CompletedAtUtc);

    public static UrlIntakeDto ToIntakeDto(Content content) => new(
        content.Id,
        content.OriginalUrl!,
        content.NormalizedUrl!,
        content.SourceKind!.Value.ToString(),
        content.IntakeStatus!.Value.ToString(),
        false,
        content.DetailContent,
        !string.IsNullOrWhiteSpace(content.DetailContent),
        content.InstagramContentType?.ToString(),
        content.ManualCaption,
        content.PinnedAuthorCommentState?.ToString(),
        content.PinnedAuthorCommentText,
        content.SourceAcquisitionMode?.ToString(),
        content.LinkedMedia.Select(link => link.MediaAssetId).Order().ToArray());
}
