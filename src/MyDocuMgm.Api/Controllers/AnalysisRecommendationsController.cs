using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.AnalysisRecommendations;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/analysis-recommendations")]
public sealed class AnalysisRecommendationsController(
    AnalysisRecommendationService service,
    ManualAnalysisRecommendationService manualService) : ControllerBase
{
    [HttpPost]
    public Task<AnalysisRecommendationRunDto> Generate(
        Guid contentId,
        [FromBody] RequestAnalysisRecommendationsRequest request,
        CancellationToken cancellationToken) =>
        service.RequestAsync(contentId, request, cancellationToken);

    [HttpGet("latest")]
    public async Task<ActionResult<AnalysisRecommendationRunDto>> Latest(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLatestAsync(contentId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet]
    public Task<IReadOnlyList<AnalysisRecommendationRunDto>> History(
        Guid contentId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(contentId, limit, cancellationToken);

    [HttpPost("manual-prompt")]
    public Task<ManualRecommendationPromptDto> CreateManualPrompt(
        Guid contentId,
        [FromBody] CreateManualRecommendationPromptRequest request,
        CancellationToken cancellationToken) =>
        manualService.CreatePromptAsync(contentId, request, cancellationToken);

    [HttpPost("manual-import")]
    public Task<AnalysisRecommendationRunDto> ImportManualResponse(
        Guid contentId,
        [FromBody] ImportManualAnalysisRecommendationsRequest request,
        CancellationToken cancellationToken) =>
        manualService.ImportAsync(contentId, request, cancellationToken);

    [HttpPut("{runId:guid}/decisions")]
    public Task<AnalysisRecommendationDecisionResult> Decide(
        Guid contentId,
        Guid runId,
        [FromBody] SaveAnalysisRecommendationDecisionsRequest request,
        CancellationToken cancellationToken) =>
        service.DecideAsync(contentId, runId, request, cancellationToken);
}
