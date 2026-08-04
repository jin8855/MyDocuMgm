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
    public IntakeStatus? IntakeStatus { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public Category Category { get; set; } = null!;
    public ICollection<ContentStep> Steps { get; set; } = [];
    public ICollection<ContentTag> ContentTags { get; set; } = [];
    public ICollection<MediaAsset> MediaAssets { get; set; } = [];
    public ICollection<ContentMediaLink> LinkedMedia { get; set; } = [];
    public ICollection<SourceEvidence> SourceEvidence { get; set; } = [];
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
        ContentSourceKind sourceKind)
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
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.URL_ACCEPTED;
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
        IntakeStatus = global::MyDocuMgm.Domain.IntakeStatus.CONTENT_READY;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeCategory(Guid categoryId)
    {
        if (CategoryId != categoryId && HasDetails())
        {
            throw new DomainRuleException(
                "CATEGORY_DETAIL_CONFLICT",
                "현재 분류의 상세정보가 있어 분류를 변경할 수 없습니다. 상세정보를 명시적으로 제거한 뒤 다시 시도하세요.");
        }

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
