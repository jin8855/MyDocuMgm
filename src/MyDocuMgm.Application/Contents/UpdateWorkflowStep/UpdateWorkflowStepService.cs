using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.UpdateWorkflowStep;

public sealed record UpdateWorkflowStepRequest(WorkflowStep Step, string RowVersion);

public sealed class UpdateWorkflowStepService(IContentRepository repository)
{
    public async Task<ContentDetail> ExecuteAsync(
        Guid contentId,
        UpdateWorkflowStepRequest request,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(contentId, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        ContentService.EnsureRowVersion(content, request.RowVersion);
        content.MoveTo(request.Step);
        await repository.SaveChangesAsync(cancellationToken);
        return ContentService.Map(content);
    }
}
