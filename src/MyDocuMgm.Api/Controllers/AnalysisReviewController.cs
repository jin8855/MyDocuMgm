using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.ReviewAnalysis;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/analysis-review")]
public sealed class AnalysisReviewController(AnalysisReviewService service) : ControllerBase
{
    [HttpPut]
    public Task<ContentDetail> Save(
        Guid contentId,
        [FromBody] SaveAnalysisReviewRequest request,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(contentId, request, cancellationToken);
}
