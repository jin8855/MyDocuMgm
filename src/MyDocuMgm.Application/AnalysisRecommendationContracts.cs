using MyDocuMgm.Domain;
using System.Text.Json.Serialization;

namespace MyDocuMgm.Application.AnalysisRecommendations;

public enum AnalysisRecommendationFailureKind
{
    UNAVAILABLE,
    INVALID_RESPONSE,
    CANCELLED,
    PROCESSING
}

public sealed class AnalysisRecommendationException(
    AnalysisRecommendationFailureKind kind,
    string code,
    string message) : InvalidOperationException(message)
{
    public AnalysisRecommendationFailureKind Kind { get; } = kind;
    public string Code { get; } = code;
}

public sealed record RequestAnalysisRecommendationsRequest(string IdempotencyKey);

public sealed record AnalysisRecommendationDecisionInput(
    Guid ItemId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationDecision Decision,
    string? ModifiedValue);

public sealed record SaveAnalysisRecommendationDecisionsRequest(
    string ContentRowVersion,
    IReadOnlyList<AnalysisRecommendationDecisionInput> Decisions);

public sealed record AnalysisRecommendationEvidenceDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationEvidenceType EvidenceType,
    Guid? SourceEvidenceId,
    string Excerpt);

public sealed record AnalysisRecommendationItemDto(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationKind Kind,
    string RecommendedValue,
    string Reason,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationConfidence Confidence,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationDecision Decision,
    string? ModifiedValue,
    DateTime? DecidedAtUtc,
    IReadOnlyList<AnalysisRecommendationEvidenceDto> Evidence);

public sealed record AnalysisRecommendationRunDto(
    Guid Id,
    Guid ContentId,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AnalysisRecommendationRunStatus Status,
    string ProviderIdentifier,
    string? ModelVersion,
    string? ErrorCode,
    string RowVersion,
    IReadOnlyList<AnalysisRecommendationItemDto> Items);

public sealed record AnalysisRecommendationDecisionResult(
    ContentDetail Content,
    AnalysisRecommendationRunDto Run);

public sealed record AnalysisRecommendationEvidenceInput(
    AnalysisRecommendationEvidenceType EvidenceType,
    Guid? SourceEvidenceId,
    string Text);

public sealed record AnalysisRecommendationProviderInput(
    Guid ContentId,
    string CurrentTitle,
    string? CurrentSummary,
    Guid CurrentCategoryId,
    IReadOnlyList<string> CurrentTags,
    IReadOnlyList<CategoryDefinition> AllowedCategories,
    IReadOnlyList<AnalysisRecommendationEvidenceInput> Evidence);

public sealed record AnalysisRecommendationProviderItem(
    AnalysisRecommendationKind Kind,
    string RecommendedValue,
    string Reason,
    AnalysisRecommendationConfidence Confidence,
    IReadOnlyList<AnalysisRecommendationEvidenceInput> Evidence);

public sealed record AnalysisRecommendationProviderResult(
    bool IsPartial,
    string? ModelVersion,
    IReadOnlyList<AnalysisRecommendationProviderItem> Items);

public interface IAnalysisRecommendationProvider
{
    string ProviderIdentifier { get; }

    Task<AnalysisRecommendationProviderResult> RecommendAsync(
        AnalysisRecommendationProviderInput input,
        CancellationToken cancellationToken);
}

public interface IAnalysisRecommendationRepository : IContentTagRepository
{
    Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken);
    Task<AnalysisRecommendationRun?> FindByIdempotencyKeyAsync(
        Guid contentId,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<AnalysisRecommendationRun?> FindLatestAsync(Guid contentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnalysisRecommendationRun>> ListAsync(
        Guid contentId,
        int limit,
        CancellationToken cancellationToken);
    Task<AnalysisRecommendationRun?> FindRunForDecisionAsync(
        Guid contentId,
        Guid runId,
        CancellationToken cancellationToken);
    Task AddRunAsync(AnalysisRecommendationRun run, CancellationToken cancellationToken);
    Task AddItemsAsync(
        IReadOnlyCollection<AnalysisRecommendationItem> items,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
