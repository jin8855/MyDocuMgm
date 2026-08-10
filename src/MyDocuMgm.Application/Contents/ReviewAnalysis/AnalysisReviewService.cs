using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.ReviewAnalysis;

public sealed record SaveAnalysisReviewRequest(
    string Title,
    string? ShortSummary,
    bool Complete,
    string RowVersion);

public sealed class AnalysisReviewService(IContentRepository repository)
{
    public async Task<ContentDetail> ExecuteAsync(
        Guid contentId,
        SaveAnalysisReviewRequest request,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(contentId, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");

        ContentService.EnsureRowVersion(content, request.RowVersion);
        EnsureManualIntakeReady(content);
        Validate(request);
        EnsureReviewable(content.CurrentWorkflowStep);

        content.Title = request.Title.Trim();
        content.ShortSummary = string.IsNullOrWhiteSpace(request.ShortSummary)
            ? null
            : request.ShortSummary.Trim();

        if (content.CurrentWorkflowStep == WorkflowStep.URL)
        {
            content.MoveTo(WorkflowStep.ANALYSIS_REVIEW);
        }

        if (request.Complete)
        {
            content.MoveTo(WorkflowStep.CATEGORY_EDIT);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ContentService.Map(content);
    }

    private static void Validate(SaveAnalysisReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
        {
            throw new DomainRuleException("INVALID_TITLE", "제목은 1~200자여야 합니다.");
        }

        if (request.ShortSummary?.Length > 500)
        {
            throw new DomainRuleException("ANALYSIS_SUMMARY_TOO_LONG", "요약은 500자 이하여야 합니다.");
        }
    }

    private static void EnsureManualIntakeReady(Content content)
    {
        var commentIsValid = content.PinnedAuthorCommentState switch
        {
            PinnedAuthorCommentState.NONE => string.IsNullOrWhiteSpace(content.PinnedAuthorCommentText),
            PinnedAuthorCommentState.PRESENT => !string.IsNullOrWhiteSpace(content.PinnedAuthorCommentText),
            _ => false
        };
        if (content.SourceKind != ContentSourceKind.INSTAGRAM ||
            content.InstagramContentType is null ||
            content.IntakeStatus != IntakeStatus.CONTENT_READY ||
            content.SourceAcquisitionMode != SourceAcquisitionMode.MANUAL ||
            string.IsNullOrWhiteSpace(content.ManualCaption) ||
            !commentIsValid)
        {
            throw new DomainRuleException(
                "MANUAL_INSTAGRAM_INTAKE_NOT_READY",
                "수동 Instagram 접수를 완료한 뒤 분석 검토를 진행해 주세요.");
        }
    }

    private static void EnsureReviewable(WorkflowStep step)
    {
        if (step is not (WorkflowStep.URL or WorkflowStep.ANALYSIS_REVIEW))
        {
            throw new DomainRuleException(
                "ANALYSIS_REVIEW_ALREADY_COMPLETED",
                "분석 검토를 완료한 콘텐츠는 이 화면에서 다시 저장할 수 없습니다.");
        }
    }
}
