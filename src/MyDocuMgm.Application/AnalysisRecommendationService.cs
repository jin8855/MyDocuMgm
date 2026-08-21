using MyDocuMgm.Application.Contents.ReviewAnalysis;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.AnalysisRecommendations;

public sealed class AnalysisRecommendationService(
    IAnalysisRecommendationRepository repository,
    IAnalysisRecommendationProvider provider,
    TimeProvider timeProvider)
{
    public async Task<AnalysisRecommendationRunDto> RequestAsync(
        Guid contentId,
        RequestAnalysisRecommendationsRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await repository.FindByIdempotencyKeyAsync(contentId, idempotencyKey, cancellationToken);
        if (existing is not null) return Map(existing);

        var content = await repository.FindContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        AnalysisReviewService.EnsureIntakeReady(content);
        AnalysisReviewService.EnsureReviewable(content.CurrentWorkflowStep);

        var run = new AnalysisRecommendationRun
        {
            ContentId = content.Id,
            Content = content,
            RequestedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ProviderIdentifier = NormalizeRequired(
                provider.ProviderIdentifier,
                AnalysisRecommendationRun.ProviderIdentifierMaxLength,
                "ANALYSIS_PROVIDER_IDENTIFIER_INVALID"),
            IdempotencyKey = idempotencyKey
        };
        await repository.AddRunAsync(run, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            var providerInput = BuildProviderInput(content);
            var result = await provider.RecommendAsync(providerInput, cancellationToken);
            AddValidatedItems(run, result, providerInput.Evidence);
            await repository.AddItemsAsync(run.Items.ToArray(), cancellationToken);
            run.ModelVersion = NormalizeOptional(result.ModelVersion, AnalysisRecommendationRun.ModelVersionMaxLength);
            var completeKinds = new[]
            {
                AnalysisRecommendationKind.TITLE,
                AnalysisRecommendationKind.SUMMARY,
                AnalysisRecommendationKind.CATEGORY,
                AnalysisRecommendationKind.TAG
            };
            var isPartial = result.IsPartial || completeKinds.Any(kind => run.Items.All(item => item.Kind != kind));
            run.Complete(isPartial, timeProvider.GetUtcNow().UtcDateTime);
            await repository.SaveChangesAsync(cancellationToken);
            return Map(run);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Cancel("ANALYSIS_RECOMMENDATION_CANCELLED", timeProvider.GetUtcNow().UtcDateTime);
            await repository.SaveChangesAsync(CancellationToken.None);
            throw new AnalysisRecommendationException(
                AnalysisRecommendationFailureKind.CANCELLED,
                "ANALYSIS_RECOMMENDATION_CANCELLED",
                "추천 요청이 취소되었습니다.");
        }
        catch (AnalysisRecommendationException exception)
        {
            run.Fail(exception.Code, timeProvider.GetUtcNow().UtcDateTime);
            await repository.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            if (run.Status == AnalysisRecommendationRunStatus.REQUESTED)
            {
                run.Fail("ANALYSIS_PROVIDER_FAILED", timeProvider.GetUtcNow().UtcDateTime);
                try
                {
                    await repository.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // 원래 처리 오류를 안전한 제품 오류로 유지한다.
                }
            }
            throw new AnalysisRecommendationException(
                AnalysisRecommendationFailureKind.PROCESSING,
                "ANALYSIS_PROVIDER_FAILED",
                "추천을 생성하지 못했습니다. 직접 작성으로 계속할 수 있습니다.");
        }
    }

    public async Task<AnalysisRecommendationRunDto?> GetLatestAsync(Guid contentId, CancellationToken cancellationToken)
    {
        _ = await repository.FindContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        var run = await repository.FindLatestAsync(contentId, cancellationToken);
        return run is null ? null : Map(run);
    }

    public async Task<IReadOnlyList<AnalysisRecommendationRunDto>> ListAsync(
        Guid contentId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 20)
        {
            throw new DomainRuleException("INVALID_RECOMMENDATION_HISTORY_LIMIT", "이력 개수는 1~20이어야 합니다.");
        }
        _ = await repository.FindContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        return (await repository.ListAsync(contentId, limit, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<AnalysisRecommendationDecisionResult> DecideAsync(
        Guid contentId,
        Guid runId,
        SaveAnalysisRecommendationDecisionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Decisions.Count == 0 || request.Decisions.Select(value => value.ItemId).Distinct().Count() != request.Decisions.Count)
        {
            throw new DomainRuleException("INVALID_ANALYSIS_DECISIONS", "서로 다른 추천 항목을 하나 이상 선택해 주세요.");
        }

        var run = await repository.FindRunForDecisionAsync(contentId, runId, cancellationToken)
            ?? throw new NotFoundException("추천 이력을 찾을 수 없습니다.");
        if (run.ContentId != contentId)
        {
            throw new DomainRuleException("ANALYSIS_RECOMMENDATION_CONTENT_MISMATCH", "다른 자료의 추천은 적용할 수 없습니다.");
        }
        if (run.Status is not (AnalysisRecommendationRunStatus.SUCCEEDED or AnalysisRecommendationRunStatus.PARTIALLY_SUCCEEDED))
        {
            throw new DomainRuleException("ANALYSIS_RECOMMENDATION_NOT_DECIDABLE", "완료된 추천 항목만 선택할 수 있습니다.");
        }

        var itemsById = run.Items.ToDictionary(value => value.Id);
        foreach (var decision in request.Decisions)
        {
            if (!itemsById.ContainsKey(decision.ItemId))
            {
                throw new DomainRuleException("ANALYSIS_RECOMMENDATION_ITEM_MISMATCH", "다른 추천 요청의 항목은 적용할 수 없습니다.");
            }
        }

        var replay = request.Decisions.All(value => itemsById[value.ItemId].HasDecision(value.Decision, value.ModifiedValue));
        if (replay)
        {
            return new AnalysisRecommendationDecisionResult(ContentService.Map(run.Content), Map(run));
        }

        ContentService.EnsureRowVersion(run.Content, request.ContentRowVersion);
        var title = run.Content.Title;
        var summary = run.Content.ShortSummary;
        var categoryId = run.Content.CategoryId;
        var tags = run.Content.ContentTags.Select(value => value.Tag.Name).ToList();
        var decidedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var decision in request.Decisions)
        {
            var item = itemsById[decision.ItemId];
            if (item.Decision != AnalysisRecommendationDecision.PENDING)
            {
                if (item.HasDecision(decision.Decision, decision.ModifiedValue))
                {
                    continue;
                }
                throw new DomainRuleException("ANALYSIS_DECISION_ALREADY_RECORDED", "이미 기록된 결정과 다른 요청입니다.");
            }
            if (decision.Decision == AnalysisRecommendationDecision.PENDING)
            {
                throw new DomainRuleException("ANALYSIS_DECISION_REQUIRED", "적용, 직접 수정 또는 사용 안 함을 선택해 주세요.");
            }

            var value = decision.Decision == AnalysisRecommendationDecision.MODIFIED
                ? decision.ModifiedValue?.Trim()
                : item.RecommendedValue.Trim();
            if (decision.Decision is AnalysisRecommendationDecision.APPLIED or AnalysisRecommendationDecision.MODIFIED)
            {
                switch (item.Kind)
                {
                    case AnalysisRecommendationKind.TITLE:
                        title = ValidateTitle(value);
                        break;
                    case AnalysisRecommendationKind.SUMMARY:
                        summary = ValidateSummary(value);
                        break;
                    case AnalysisRecommendationKind.CATEGORY:
                        categoryId = ValidateCategory(value);
                        break;
                    case AnalysisRecommendationKind.TAG:
                        tags.Add(ValidateTag(value));
                        break;
                }
            }
            item.Decide(decision.Decision, decision.ModifiedValue, decidedAtUtc);
        }

        run.Content.Title = title;
        run.Content.ShortSummary = summary;
        run.Content.CategoryId = categoryId;
        run.Content.UpdatedAtUtc = decidedAtUtc;
        await ContentTagUpdater.ReplaceAsync(run.Content, tags, repository, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new AnalysisRecommendationDecisionResult(ContentService.Map(run.Content), Map(run));
    }

    private static AnalysisRecommendationProviderInput BuildProviderInput(Content content)
    {
        var evidence = new List<AnalysisRecommendationEvidenceInput>();
        AddEvidence(evidence, AnalysisRecommendationEvidenceType.DETAIL_CONTENT, null, content.DetailContent);
        AddEvidence(evidence, AnalysisRecommendationEvidenceType.MANUAL_CAPTION, null, content.ManualCaption);
        AddEvidence(evidence, AnalysisRecommendationEvidenceType.PINNED_AUTHOR_COMMENT, null, content.PinnedAuthorCommentText);
        foreach (var source in content.SourceEvidence)
        {
            AddEvidence(evidence, AnalysisRecommendationEvidenceType.SOURCE_EVIDENCE, source.Id, source.SourceTitle);
        }
        return new AnalysisRecommendationProviderInput(
            content.Id,
            content.Title,
            content.ShortSummary,
            content.CategoryId,
            content.ContentTags.Select(value => value.Tag.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            CategoryCatalog.All,
            evidence);
    }

    private static void AddEvidence(
        ICollection<AnalysisRecommendationEvidenceInput> target,
        AnalysisRecommendationEvidenceType type,
        Guid? sourceEvidenceId,
        string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            target.Add(new(type, sourceEvidenceId, Truncate(text.Trim(), AnalysisRecommendationEvidence.ExcerptMaxLength)));
        }
    }

    private static void AddValidatedItems(
        AnalysisRecommendationRun run,
        AnalysisRecommendationProviderResult result,
        IReadOnlyList<AnalysisRecommendationEvidenceInput> allowedEvidence)
    {
        if (result.Items.Count == 0) throw InvalidProviderResponse();
        if (result.Items.Count(value => value.Kind == AnalysisRecommendationKind.TITLE) > 1 ||
            result.Items.Count(value => value.Kind == AnalysisRecommendationKind.SUMMARY) > 1 ||
            result.Items.Count(value => value.Kind == AnalysisRecommendationKind.CATEGORY) > 1)
        {
            throw InvalidProviderResponse();
        }

        foreach (var providerItem in result.Items)
        {
            try
            {
                if (!Enum.IsDefined(providerItem.Kind) || !Enum.IsDefined(providerItem.Confidence))
                {
                    throw InvalidProviderResponse();
                }
                var item = new AnalysisRecommendationItem
                {
                    Run = run,
                    RunId = run.Id,
                    Kind = providerItem.Kind,
                    RecommendedValue = ValidateRecommendedValue(providerItem.Kind, providerItem.RecommendedValue),
                    Reason = NormalizeRequired(providerItem.Reason, AnalysisRecommendationItem.ReasonMaxLength, "ANALYSIS_PROVIDER_REASON_INVALID"),
                    Confidence = providerItem.Confidence
                };
                foreach (var providerEvidence in providerItem.Evidence)
                {
                    if (!Enum.IsDefined(providerEvidence.EvidenceType))
                    {
                        throw InvalidProviderResponse();
                    }
                    var excerpt = NormalizeRequired(
                        providerEvidence.Text,
                        AnalysisRecommendationEvidence.ExcerptMaxLength,
                        "ANALYSIS_PROVIDER_EVIDENCE_INVALID");
                    var isAllowedEvidence = allowedEvidence.Any(value =>
                        value.EvidenceType == providerEvidence.EvidenceType &&
                        value.SourceEvidenceId == providerEvidence.SourceEvidenceId &&
                        value.Text.Contains(excerpt, StringComparison.Ordinal));
                    if (!isAllowedEvidence) throw InvalidProviderResponse();
                    item.Evidence.Add(new AnalysisRecommendationEvidence
                    {
                        Item = item,
                        ItemId = item.Id,
                        SourceEvidenceId = providerEvidence.SourceEvidenceId,
                        EvidenceType = providerEvidence.EvidenceType,
                        Excerpt = excerpt
                    });
                }
                run.Items.Add(item);
            }
            catch (DomainRuleException)
            {
                throw InvalidProviderResponse();
            }
        }
    }

    private static string ValidateRecommendedValue(AnalysisRecommendationKind kind, string? value) => kind switch
    {
        AnalysisRecommendationKind.TITLE => ValidateTitle(value),
        AnalysisRecommendationKind.SUMMARY => ValidateSummary(value) ?? throw InvalidProviderResponse(),
        AnalysisRecommendationKind.CATEGORY => ValidateCategory(value).ToString(),
        AnalysisRecommendationKind.TAG => ValidateTag(value),
        _ => throw InvalidProviderResponse()
    };

    private static string ValidateTitle(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200)
            throw new DomainRuleException("INVALID_TITLE", "제목은 1~200자여야 합니다.");
        return normalized;
    }

    private static string? ValidateSummary(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > 500)
            throw new DomainRuleException("ANALYSIS_SUMMARY_TOO_LONG", "요약은 500자 이하여야 합니다.");
        return normalized;
    }

    private static Guid ValidateCategory(string? value)
    {
        if (!Guid.TryParse(value, out var categoryId))
            throw new DomainRuleException("CATEGORY_NOT_FOUND", "고정 분류를 찾을 수 없습니다.");
        _ = CategoryCatalog.Get(categoryId);
        return categoryId;
    }

    private static string ValidateTag(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 80 || TagNormalizer.Normalize(normalized).Length > 80)
            throw new DomainRuleException("INVALID_TAG", "태그는 1~80자여야 합니다.");
        return normalized;
    }

    private static string NormalizeIdempotencyKey(string value) =>
        NormalizeRequired(value, AnalysisRecommendationRun.IdempotencyKeyMaxLength, "INVALID_IDEMPOTENCY_KEY");

    private static string NormalizeRequired(string? value, int maxLength, string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
            throw new DomainRuleException(code, "추천 계약 값이 허용 범위를 벗어났습니다.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : Truncate(normalized, maxLength);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static AnalysisRecommendationException InvalidProviderResponse() =>
        new(AnalysisRecommendationFailureKind.INVALID_RESPONSE, "ANALYSIS_PROVIDER_RESPONSE_INVALID", "추천 결과를 확인할 수 없습니다. 직접 작성으로 계속할 수 있습니다.");

    internal static AnalysisRecommendationRunDto Map(AnalysisRecommendationRun run) =>
        new(
            run.Id,
            run.ContentId,
            run.RequestedAtUtc,
            run.CompletedAtUtc,
            run.Status,
            run.ProviderIdentifier,
            run.ModelVersion,
            run.ErrorCode,
            Convert.ToBase64String(run.RowVersion),
            run.Items.OrderBy(value => value.Kind).ThenBy(value => value.Id).Select(item =>
                new AnalysisRecommendationItemDto(
                    item.Id,
                    item.Kind,
                    item.RecommendedValue,
                    item.Reason,
                    item.Confidence,
                    item.Decision,
                    item.ModifiedValue,
                    item.DecidedAtUtc,
                    item.Evidence.Select(evidence => new AnalysisRecommendationEvidenceDto(
                        evidence.EvidenceType,
                        evidence.SourceEvidenceId,
                        evidence.Excerpt)).ToArray())).ToArray());
}
