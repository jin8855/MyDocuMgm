namespace MyDocuMgm.Domain;

public enum AnalysisRecommendationRunStatus
{
    REQUESTED,
    SUCCEEDED,
    PARTIALLY_SUCCEEDED,
    FAILED,
    CANCELLED
}

public enum AnalysisRecommendationKind
{
    TITLE,
    SUMMARY,
    CATEGORY,
    TAG
}

public enum AnalysisRecommendationConfidence
{
    LOW,
    MEDIUM,
    HIGH
}

public enum AnalysisRecommendationDecision
{
    PENDING,
    APPLIED,
    MODIFIED,
    REJECTED
}

public enum AnalysisRecommendationEvidenceType
{
    DETAIL_CONTENT,
    MANUAL_CAPTION,
    PINNED_AUTHOR_COMMENT,
    SOURCE_EVIDENCE
}

public sealed class AnalysisRecommendationRun
{
    public const int ProviderIdentifierMaxLength = 80;
    public const int ModelVersionMaxLength = 120;
    public const int IdempotencyKeyMaxLength = 100;
    public const int ErrorCodeMaxLength = 80;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public AnalysisRecommendationRunStatus Status { get; set; } = AnalysisRecommendationRunStatus.REQUESTED;
    public string ProviderIdentifier { get; set; } = string.Empty;
    public string? ModelVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Content Content { get; set; } = null!;
    public ICollection<AnalysisRecommendationItem> Items { get; set; } = [];

    public void Complete(bool partial, DateTime? completedAtUtc = null)
    {
        EnsureRequested();
        Status = partial
            ? AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED
            : AnalysisRecommendationRunStatus.SUCCEEDED;
        ErrorCode = null;
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;
    }

    public void Fail(string errorCode, DateTime? completedAtUtc = null)
    {
        EnsureRequested();
        Status = AnalysisRecommendationRunStatus.FAILED;
        ErrorCode = NormalizeErrorCode(errorCode);
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;
    }

    public void Cancel(string errorCode, DateTime? completedAtUtc = null)
    {
        EnsureRequested();
        Status = AnalysisRecommendationRunStatus.CANCELLED;
        ErrorCode = NormalizeErrorCode(errorCode);
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;
    }

    private void EnsureRequested()
    {
        if (Status != AnalysisRecommendationRunStatus.REQUESTED)
        {
            throw new DomainRuleException(
                "ANALYSIS_RECOMMENDATION_RUN_FINALIZED",
                "이미 완료된 추천 요청의 상태는 변경할 수 없습니다.");
        }
    }

    private static string NormalizeErrorCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        return trimmed.Length <= ErrorCodeMaxLength ? trimmed : trimmed[..ErrorCodeMaxLength];
    }
}

public sealed class AnalysisRecommendationItem
{
    public const int RecommendedValueMaxLength = 500;
    public const int ReasonMaxLength = 500;
    public const int ModifiedValueMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public AnalysisRecommendationKind Kind { get; set; }
    public string RecommendedValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public AnalysisRecommendationConfidence Confidence { get; set; }
    public AnalysisRecommendationDecision Decision { get; set; } = AnalysisRecommendationDecision.PENDING;
    public string? ModifiedValue { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public AnalysisRecommendationRun Run { get; set; } = null!;
    public ICollection<AnalysisRecommendationEvidence> Evidence { get; set; } = [];

    public void Decide(AnalysisRecommendationDecision decision, string? modifiedValue, DateTime decidedAtUtc)
    {
        if (decision == AnalysisRecommendationDecision.PENDING)
        {
            throw new DomainRuleException(
                "ANALYSIS_DECISION_REQUIRED",
                "추천 항목에 적용, 수정 또는 사용 안 함을 선택해 주세요.");
        }

        var normalized = decision == AnalysisRecommendationDecision.MODIFIED
            ? modifiedValue?.Trim()
            : null;
        if (decision == AnalysisRecommendationDecision.MODIFIED && string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainRuleException(
                "ANALYSIS_MODIFIED_VALUE_REQUIRED",
                "직접 수정한 값을 입력해 주세요.");
        }

        if (normalized?.Length > ModifiedValueMaxLength)
        {
            throw new DomainRuleException(
                "ANALYSIS_MODIFIED_VALUE_TOO_LONG",
                $"직접 수정한 값은 {ModifiedValueMaxLength:N0}자 이하여야 합니다.");
        }

        if (Decision != AnalysisRecommendationDecision.PENDING)
        {
            if (Decision == decision && string.Equals(ModifiedValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            throw new DomainRuleException(
                "ANALYSIS_DECISION_ALREADY_RECORDED",
                "이미 결정한 추천 항목은 다른 결정으로 변경할 수 없습니다.");
        }

        Decision = decision;
        ModifiedValue = normalized;
        DecidedAtUtc = decidedAtUtc;
    }

    public bool HasDecision(AnalysisRecommendationDecision decision, string? modifiedValue)
    {
        var normalized = decision == AnalysisRecommendationDecision.MODIFIED
            ? modifiedValue?.Trim()
            : null;
        return Decision == decision && string.Equals(ModifiedValue, normalized, StringComparison.Ordinal);
    }
}

public sealed class AnalysisRecommendationEvidence
{
    public const int ExcerptMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public Guid? SourceEvidenceId { get; set; }
    public AnalysisRecommendationEvidenceType EvidenceType { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public AnalysisRecommendationItem Item { get; set; } = null!;
    public SourceEvidence? SourceEvidence { get; set; }
}
