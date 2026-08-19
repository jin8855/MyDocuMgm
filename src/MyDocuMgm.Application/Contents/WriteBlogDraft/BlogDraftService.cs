using System.Text.Json.Serialization;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.WriteBlogDraft;

public sealed record BlogDraftMediaItem(
    Guid Id,
    Guid OwnerContentId,
    string OriginalFileName,
    string ThumbnailUrl,
    string MimeType,
    int Width,
    int Height);

public sealed record BlogDraftDto(
    Guid ContentId,
    WorkflowStep CurrentWorkflowStep,
    string ContentRowVersion,
    bool HasSavedDraft,
    string? DraftRowVersion,
    string Title,
    string Body,
    int TitleMaxLength,
    int BodyMaxLength,
    string AnalysisTitle,
    string? ShortSummary,
    string CategoryDisplayName,
    bool RequiresExternalSourceReuseConfirmation,
    bool ExternalSourceReuseConfirmed,
    IReadOnlyList<BlogDraftMediaItem> LinkedMedia);

public sealed class SaveBlogDraftRequest
{
    private string? _title;
    private string? _body;

    public string? Title
    {
        get => _title;
        init
        {
            _title = value;
            HasTitle = true;
        }
    }

    public string? Body
    {
        get => _body;
        init
        {
            _body = value;
            HasBody = true;
        }
    }

    public bool Complete { get; init; }
    public bool ConfirmExternalSourceReuse { get; init; }
    public string? ContentRowVersion { get; init; }
    public string? DraftRowVersion { get; init; }

    [JsonIgnore]
    public bool HasTitle { get; private set; }

    [JsonIgnore]
    public bool HasBody { get; private set; }
}

public interface IBlogDraftRepository
{
    Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken);
    void Add(BlogDraft draft);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class BlogDraftService(IBlogDraftRepository repository)
{
    public async Task<BlogDraftDto> GetAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        EnsureCurrentStage(content.CurrentWorkflowStep);
        return Map(content);
    }

    public async Task<BlogDraftDto> GetCompletionAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        if (content.CurrentWorkflowStep != WorkflowStep.COMPLETED)
        {
            throw Error("COMPLETION_NOT_AVAILABLE", "완료된 콘텐츠만 완료 화면을 조회할 수 있습니다.");
        }

        if (content.BlogDraft is null)
        {
            throw Error("COMPLETION_BLOG_DRAFT_MISSING", "완료된 콘텐츠의 저장된 블로그 초안을 찾을 수 없습니다.");
        }

        return Map(content);
    }

    public async Task<BlogDraftDto> SaveAsync(
        Guid contentId,
        SaveBlogDraftRequest request,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        if (content.CurrentWorkflowStep == WorkflowStep.COMPLETED && request.Complete)
        {
            return CompleteRetry(content, request);
        }

        EnsureCurrentStage(content.CurrentWorkflowStep);
        if (string.IsNullOrWhiteSpace(request.ContentRowVersion))
        {
            throw new ConcurrencyConflictException("블로그 초안 저장에는 콘텐츠 rowversion이 필요합니다.");
        }

        ContentService.EnsureRowVersion(content, request.ContentRowVersion);
        var draft = content.BlogDraft;
        if (draft is not null)
        {
            EnsureDraftRowVersion(draft, request.DraftRowVersion);
        }

        if (content.SourceAcquisitionMode == SourceAcquisitionMode.HTTP_METADATA &&
            content.ExternalContentBlogReuseConfirmedAtUtc is null)
        {
            if (!request.ConfirmExternalSourceReuse)
            {
                throw Error(
                    "BLOG_DRAFT_SOURCE_REUSE_CONFIRMATION_REQUIRED",
                    "외부 출처의 내용을 블로그 초안에 재사용하려면 권리 확인이 필요합니다.");
            }
            content.ConfirmExternalContentBlogReuse();
        }

        var title = request.HasTitle ? request.Title ?? string.Empty : draft?.Title ?? content.Title;
        var body = request.HasBody ? request.Body ?? string.Empty : draft?.Body ?? InitialBody(content);
        ValidateLengths(title, body);
        if (request.Complete)
        {
            ValidateCompletion(title, body);
        }

        if (draft is null)
        {
            draft = new BlogDraft
            {
                ContentId = content.Id,
                Content = content,
                Title = title,
                Body = body
            };
            content.BlogDraft = draft;
            repository.Add(draft);
        }
        else
        {
            draft.Title = title;
            draft.Body = body;
            draft.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (request.Complete)
        {
            content.MoveTo(WorkflowStep.COMPLETED);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    private async Task<Content> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        await repository.FindAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");

    private static BlogDraftDto CompleteRetry(Content content, SaveBlogDraftRequest request)
    {
        var draft = content.BlogDraft
            ?? throw Error("BLOG_DRAFT_ALREADY_COMPLETED", "완료된 콘텐츠의 블로그 초안을 찾을 수 없습니다.");
        if (!request.HasTitle || !request.HasBody)
        {
            throw Error("BLOG_DRAFT_ALREADY_COMPLETED", "완료 응답 재확인에는 기존 요청의 제목과 본문이 필요합니다.");
        }

        var title = request.HasTitle ? request.Title ?? string.Empty : draft.Title;
        var body = request.HasBody ? request.Body ?? string.Empty : draft.Body;
        ValidateLengths(title, body);
        ValidateCompletion(title, body);
        if (!string.Equals(title, draft.Title, StringComparison.Ordinal) ||
            !string.Equals(body, draft.Body, StringComparison.Ordinal))
        {
            throw Error("BLOG_DRAFT_ALREADY_COMPLETED", "이미 완료된 블로그 초안은 변경할 수 없습니다.");
        }

        return Map(content);
    }

    private static void EnsureCurrentStage(WorkflowStep step)
    {
        if (step == WorkflowStep.BLOG_DRAFT)
        {
            return;
        }

        throw Error(
            (int)step < (int)WorkflowStep.BLOG_DRAFT
                ? "BLOG_DRAFT_NOT_AVAILABLE"
                : "BLOG_DRAFT_ALREADY_COMPLETED",
            (int)step < (int)WorkflowStep.BLOG_DRAFT
                ? "자료 상세 단계를 완료한 뒤 블로그 초안을 작성해 주세요."
                : "이미 완료된 블로그 초안입니다.");
    }

    private static void EnsureDraftRowVersion(BlogDraft draft, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
        {
            throw new ConcurrencyConflictException("기존 블로그 초안 저장에는 draft rowversion이 필요합니다.");
        }

        ContentService.EnsureRowVersion(new Content { RowVersion = draft.RowVersion }, supplied);
    }

    private static void ValidateLengths(string title, string body)
    {
        if (title.Length > BlogDraft.TitleMaxLength)
        {
            throw Error("BLOG_DRAFT_TITLE_TOO_LONG", $"초안 제목은 {BlogDraft.TitleMaxLength}자 이하여야 합니다.");
        }

        if (body.Length > BlogDraft.BodyMaxLength)
        {
            throw Error("BLOG_DRAFT_BODY_TOO_LONG", $"초안 본문은 {BlogDraft.BodyMaxLength}자 이하여야 합니다.");
        }
    }

    private static void ValidateCompletion(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw Error("BLOG_DRAFT_TITLE_REQUIRED", "완료하려면 초안 제목을 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw Error("BLOG_DRAFT_BODY_REQUIRED", "완료하려면 초안 본문을 입력해 주세요.");
        }
    }

    private static BlogDraftDto Map(Content content)
    {
        var draft = content.BlogDraft;
        var linkedMedia = content.LinkedMedia
            .Where(link => link.MediaAsset is { IsDeleted: false, StorageStatus: MediaStorageStatus.READY })
            .Select(link => link.MediaAsset)
            .OrderBy(media => media.SortOrder)
            .ThenBy(media => media.CreatedAtUtc)
            .Select(media => new BlogDraftMediaItem(
                media.Id,
                media.ContentId,
                media.OriginalFileName,
                $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.Width,
                media.Height))
            .ToArray();
        var category = CategoryCatalog.Get(content.CategoryId);

        return new BlogDraftDto(
            content.Id,
            content.CurrentWorkflowStep,
            Convert.ToBase64String(content.RowVersion),
            draft is not null,
            draft is null ? null : Convert.ToBase64String(draft.RowVersion),
            draft?.Title ?? content.Title,
            draft?.Body ?? InitialBody(content),
            BlogDraft.TitleMaxLength,
            BlogDraft.BodyMaxLength,
            content.Title,
            content.ShortSummary,
            category.DisplayName,
            content.SourceAcquisitionMode == SourceAcquisitionMode.HTTP_METADATA,
            content.SourceAcquisitionMode != SourceAcquisitionMode.HTTP_METADATA ||
                content.ExternalContentBlogReuseConfirmedAtUtc is not null,
            linkedMedia);
    }

    private static string InitialBody(Content content) =>
        FirstPresent(content.DetailContent, content.ManualCaption, content.ShortSummary) ?? string.Empty;

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static DomainRuleException Error(string code, string message) => new(code, message);
}
