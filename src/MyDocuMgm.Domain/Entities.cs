using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MyDocuMgm.Domain;

public sealed class Category
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<Content> Contents { get; set; } = [];
    public ICollection<CategorySearchAttribute> SearchAttributes { get; set; } = [];
}

public sealed class CategorySearchAttribute
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string AttributeKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSearchable { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public Category Category { get; set; } = null!;
}

public sealed class Content
{
    public const int ManualBodyMaxLength = 20_000;
    public const int ManualCaptionMaxLength = 20_000;
    public const int PinnedAuthorCommentMaxLength = 10_000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortSummary { get; set; }
    public string? DetailContent { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.INBOX;
    public ContentVisibility Visibility { get; set; } = ContentVisibility.PRIVATE;
    public bool IsFavorite { get; set; }
    public ExperienceStatus ExperienceStatus { get; set; } = ExperienceStatus.NONE;
    public WorkflowStep CurrentWorkflowStep { get; set; } = WorkflowStep.URL;
    public string? OriginalUrl { get; set; }
    public string? NormalizedUrl { get; set; }
    public byte[]? NormalizedUrlHash { get; set; }
    public ContentSourceKind? SourceKind { get; set; }
    public InstagramContentType? InstagramContentType { get; set; }
    public string? ManualCaption { get; set; }
    public PinnedAuthorCommentState? PinnedAuthorCommentState { get; set; }
    public string? PinnedAuthorCommentText { get; set; }
    public SourceAcquisitionMode? SourceAcquisitionMode { get; set; }
    public IntakeStatus? IntakeStatus { get; set; }
    public DateTime? ExternalContentBlogReuseConfirmedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public Category Category { get; set; } = null!;
    public BlogDraft? BlogDraft { get; set; }
    public ICollection<ContentStep> Steps { get; set; } = [];
    public ICollection<ContentTag> ContentTags { get; set; } = [];
    public ICollection<MediaAsset> MediaAssets { get; set; } = [];
    public ICollection<ContentMediaLink> LinkedMedia { get; set; } = [];
    public ICollection<SourceEvidence> SourceEvidence { get; set; } = [];
    public ICollection<ExternalFetchAttempt> ExternalFetchAttempts { get; set; } = [];
    public PlaceDetails? PlaceDetails { get; set; }
    public CookingDetails? CookingDetails { get; set; }
    public ExerciseDetails? ExerciseDetails { get; set; }
    public CleaningLaundryDetails? CleaningLaundryDetails { get; set; }
    public TravelDetails? TravelDetails { get; set; }
    public PhotoDetails? PhotoDetails { get; set; }
    public StudyDetails? StudyDetails { get; set; }
    public ProductDetails? ProductDetails { get; set; }
    public PhoneComputerDetails? PhoneComputerDetails { get; set; }
    public TipDetails? TipDetails { get; set; }
    public OtherDetails? OtherDetails { get; set; }

    public void SetStatus(ContentStatus status)
    {
        if (!ContentStatusRules.IsPhase1Selectable(status))
        {
            throw new DomainRuleException("STATUS_NOT_SELECTABLE", "Phase 1에서는 블로그 상태를 선택할 수 없습니다.");
        }

        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MoveTo(WorkflowStep next)
    {
        if (!WorkflowStepRules.CanMove(CurrentWorkflowStep, next))
        {
            throw new DomainRuleException(
                "INVALID_WORKFLOW_TRANSITION",
                $"{CurrentWorkflowStep} 단계에서 {next} 단계로 바로 이동할 수 없습니다.");
        }

        CurrentWorkflowStep = next;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AcceptUrl(
        string originalUrl,
        string normalizedUrl,
        byte[] normalizedUrlHash,
        ContentSourceKind sourceKind,
        InstagramContentType? instagramContentType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUrl);
        if (normalizedUrlHash.Length != 32)
        {
            throw new ArgumentException("정규화 URL SHA-256은 32바이트여야 합니다.", nameof(normalizedUrlHash));
        }
        OriginalUrl = originalUrl;
        NormalizedUrl = normalizedUrl;
        NormalizedUrlHash = [.. normalizedUrlHash];
        SourceKind = sourceKind;
        InstagramContentType = instagramContentType;
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.URL_ACCEPTED;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SaveManualInstagram(
        string caption,
        PinnedAuthorCommentState commentState,
        string? commentText)
    {
        if (SourceKind != ContentSourceKind.INSTAGRAM || InstagramContentType is null)
        {
            throw new DomainRuleException(
                "INSTAGRAM_INTAKE_REQUIRED",
                "Instagram 게시물 또는 Reel 접수 정보가 필요합니다.");
        }

        var trimmedCaption = caption?.Trim() ?? string.Empty;
        if (trimmedCaption.Length == 0)
        {
            throw new DomainRuleException("MANUAL_CAPTION_REQUIRED", "Caption을 직접 입력해 주세요.");
        }

        if (trimmedCaption.Length > ManualCaptionMaxLength)
        {
            throw new DomainRuleException(
                "MANUAL_CAPTION_TOO_LONG",
                $"Caption은 {ManualCaptionMaxLength:N0}자 이하여야 합니다.");
        }

        var trimmedComment = commentText?.Trim();
        if (commentState == global::MyDocuMgm.Domain.PinnedAuthorCommentState.PRESENT &&
            string.IsNullOrWhiteSpace(trimmedComment))
        {
            throw new DomainRuleException(
                "PINNED_AUTHOR_COMMENT_REQUIRED",
                "작성자가 작성한 고정 댓글 본문을 입력해 주세요.");
        }

        if (trimmedComment?.Length > PinnedAuthorCommentMaxLength)
        {
            throw new DomainRuleException(
                "PINNED_AUTHOR_COMMENT_TOO_LONG",
                $"작성자 고정 댓글은 {PinnedAuthorCommentMaxLength:N0}자 이하여야 합니다.");
        }

        ManualCaption = trimmedCaption;
        PinnedAuthorCommentState = commentState;
        PinnedAuthorCommentText = commentState == global::MyDocuMgm.Domain.PinnedAuthorCommentState.NONE
            ? null
            : trimmedComment;
        SourceAcquisitionMode = global::MyDocuMgm.Domain.SourceAcquisitionMode.MANUAL;
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.CONTENT_READY;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RequireManualInput()
    {
        if (IntakeStatus is null)
        {
            throw new DomainRuleException("URL_INTAKE_NOT_FOUND", "URL 접수 상태가 없습니다.");
        }

        if (IntakeStatus == global::MyDocuMgm.Domain.IntakeStatus.CONTENT_READY)
        {
            return;
        }

        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.MANUAL_INPUT_REQUIRED;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SaveManualBody(string body)
    {
        if (IntakeStatus is null)
        {
            throw new DomainRuleException("URL_INTAKE_NOT_FOUND", "URL 접수 상태가 없습니다.");
        }

        var trimmedBody = body?.Trim() ?? string.Empty;
        if (trimmedBody.Length == 0)
        {
            throw new DomainRuleException("MANUAL_BODY_REQUIRED", "본문을 입력해 주세요.");
        }

        if (trimmedBody.Length > ManualBodyMaxLength)
        {
            throw new DomainRuleException(
                "MANUAL_BODY_TOO_LONG",
                $"본문은 {ManualBodyMaxLength:N0}자 이하여야 합니다.");
        }

        DetailContent = trimmedBody;
        SourceAcquisitionMode = global::MyDocuMgm.Domain.SourceAcquisitionMode.MANUAL;
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.CONTENT_READY;
        ExternalContentBlogReuseConfirmedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyExternalFetch(string? title, string? description, string body)
    {
        if (SourceKind != ContentSourceKind.GENERIC)
        {
            throw new DomainRuleException(
                "EXTERNAL_FETCH_GENERIC_URL_REQUIRED",
                "일반 URL 콘텐츠에만 외부 가져오기 결과를 적용할 수 있습니다.");
        }

        if (CurrentWorkflowStep != WorkflowStep.URL)
        {
            throw new DomainRuleException(
                "URL_STAGE_ALREADY_COMPLETED",
                "URL intake data cannot be changed after the workflow leaves the URL stage.");
        }

        var trimmedBody = body?.Trim() ?? string.Empty;
        if (trimmedBody.Length == 0)
        {
            throw new DomainRuleException("EXTERNAL_FETCH_BODY_REQUIRED", "적용할 본문을 입력해 주세요.");
        }

        if (trimmedBody.Length > ManualBodyMaxLength)
        {
            throw new DomainRuleException(
                "EXTERNAL_FETCH_BODY_TOO_LONG",
                $"적용할 본문은 {ManualBodyMaxLength:N0}자 이하여야 합니다.");
        }

        var trimmedTitle = title?.Trim();
        if (trimmedTitle?.Length > 200)
        {
            throw new DomainRuleException("EXTERNAL_FETCH_TITLE_TOO_LONG", "제목은 200자 이하여야 합니다.");
        }

        var trimmedDescription = description?.Trim();
        if (trimmedDescription?.Length > 500)
        {
            throw new DomainRuleException("EXTERNAL_FETCH_DESCRIPTION_TOO_LONG", "요약은 500자 이하여야 합니다.");
        }

        if (!string.IsNullOrWhiteSpace(trimmedTitle))
        {
            Title = trimmedTitle;
        }

        ShortSummary = string.IsNullOrWhiteSpace(trimmedDescription) ? null : trimmedDescription;
        DetailContent = trimmedBody;
        SourceAcquisitionMode = global::MyDocuMgm.Domain.SourceAcquisitionMode.HTTP_METADATA;
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.CONTENT_READY;
        ExternalContentBlogReuseConfirmedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ConfirmExternalContentBlogReuse()
    {
        if (SourceAcquisitionMode == global::MyDocuMgm.Domain.SourceAcquisitionMode.HTTP_METADATA &&
            ExternalContentBlogReuseConfirmedAtUtc is null)
        {
            ExternalContentBlogReuseConfirmedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void ChangeCategory(Guid categoryId)
    {
        CategoryId = categoryId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool HasDetails() =>
        PlaceDetails is not null || CookingDetails is not null || ExerciseDetails is not null ||
        CleaningLaundryDetails is not null || TravelDetails is not null || PhotoDetails is not null ||
        StudyDetails is not null || ProductDetails is not null || PhoneComputerDetails is not null ||
        TipDetails is not null || OtherDetails is not null;

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public sealed class BlogDraft
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 20_000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public Content Content { get; set; } = null!;
}

public sealed class ContentStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? MediaAssetId { get; set; }
    public Content Content { get; set; } = null!;
    public MediaAsset? MediaAsset { get; set; }

    public void AssignMedia(MediaAsset? media)
    {
        if (media?.IsDeleted == true)
        {
            throw new DomainRuleException("DELETED_MEDIA_NOT_ASSIGNABLE", "삭제된 이미지는 단계 대표 이미지로 지정할 수 없습니다.");
        }

        if (media is not null && media.ContentId != ContentId)
        {
            throw new DomainRuleException("MEDIA_CONTENT_MISMATCH", "다른 콘텐츠의 이미지는 단계에 연결할 수 없습니다.");
        }

        MediaAsset = media;
        MediaAssetId = media?.Id;
    }
}

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public ICollection<ContentTag> ContentTags { get; set; } = [];
}

public sealed class ContentTag
{
    public Guid ContentId { get; set; }
    public Guid TagId { get; set; }
    public Content Content { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public sealed class ContentMediaLink
{
    public Guid ContentId { get; set; }
    public Guid MediaAssetId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Content Content { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}

public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int SortOrder { get; set; }
    public long? SourceTimestampMs { get; set; }
    public bool IsSelected { get; set; }
    public string? Description { get; set; }
    public bool IsPublicAllowed { get; set; }
    public MediaStorageStatus StorageStatus { get; set; } = MediaStorageStatus.PENDING;
    public string? FailureReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public Content Content { get; set; } = null!;
    public ICollection<ContentStep> ContentSteps { get; set; } = [];
    public ICollection<ContentMediaLink> LinkedContents { get; set; } = [];

    public void MarkReady() => StorageStatus = MediaStorageStatus.READY;

    public void MarkFailed(string reason)
    {
        StorageStatus = MediaStorageStatus.FAILED;
        FailureReason = reason;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
    }
}

public sealed class SourceEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceTitle { get; set; }
    public string? SourceReference { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public Content Content { get; set; } = null!;
}

public sealed class ExternalFetchAttempt
{
    public const int ExtractedTextMaxLength = 20_000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public int AttemptNumber { get; set; }
    public ExternalFetchStatus Status { get; set; } = ExternalFetchStatus.STARTED;
    public string? FinalUrl { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseMimeType { get; set; }
    public long? ResponseBytes { get; set; }
    public string? ContentSha256 { get; set; }
    public string? ETag { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? PageTitle { get; set; }
    public string? PageDescription { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? ExtractedText { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Content Content { get; set; } = null!;

    public void Succeed(
        string finalUrl,
        int httpStatusCode,
        string responseMimeType,
        long responseBytes,
        string contentSha256,
        string? etag,
        DateTime? lastModifiedAtUtc,
        string? pageTitle,
        string? pageDescription,
        string? authorName,
        DateTime? publishedAtUtc,
        string extractedText)
    {
        Status = ExternalFetchStatus.SUCCEEDED;
        FinalUrl = finalUrl;
        HttpStatusCode = httpStatusCode;
        ResponseMimeType = responseMimeType;
        ResponseBytes = responseBytes;
        ContentSha256 = contentSha256;
        ETag = etag;
        LastModifiedAtUtc = lastModifiedAtUtc;
        PageTitle = pageTitle;
        PageDescription = pageDescription;
        AuthorName = authorName;
        PublishedAtUtc = publishedAtUtc;
        ExtractedText = extractedText;
        ErrorCode = null;
        ErrorMessage = null;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string code, string message)
    {
        Status = ExternalFetchStatus.FAILED;
        ErrorCode = code;
        ErrorMessage = message;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string code, string message)
    {
        Status = ExternalFetchStatus.CANCELLED;
        ErrorCode = code;
        ErrorMessage = message;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkApplied()
    {
        if (Status == ExternalFetchStatus.APPLIED)
        {
            return;
        }

        if (Status != ExternalFetchStatus.SUCCEEDED)
        {
            throw new DomainRuleException(
                "EXTERNAL_FETCH_NOT_APPLICABLE",
                "성공한 외부 가져오기만 적용할 수 있습니다.");
        }

        Status = ExternalFetchStatus.APPLIED;
        CompletedAtUtc = DateTime.UtcNow;
    }
}

public static partial class TagNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex RepeatedWhitespace();

    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var unicode = value.Normalize(NormalizationForm.FormKC);
        var collapsed = RepeatedWhitespace().Replace(unicode.Trim(), " ");
        return collapsed.ToUpper(CultureInfo.InvariantCulture);
    }
}
