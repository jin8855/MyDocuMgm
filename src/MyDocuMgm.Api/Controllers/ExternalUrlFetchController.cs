using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.ExternalFetch;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/url-intakes/{contentId:guid}/external-fetches")]
public sealed class ExternalUrlFetchController(ExternalFetchService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ExternalFetchAttemptDto>> Start(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var result = await service.StartAsync(contentId, cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { contentId, attemptId = result.Id },
            result);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<ExternalFetchAttemptDto>> Latest(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLatestAsync(contentId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("{attemptId:guid}")]
    public Task<ExternalFetchAttemptDto> Get(
        Guid contentId,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        service.GetAsync(contentId, attemptId, cancellationToken);

    [HttpPut("{attemptId:guid}/apply")]
    public Task<ExternalFetchApplyDto> Apply(
        Guid contentId,
        Guid attemptId,
        [FromBody] ApplyExternalFetchRequest request,
        CancellationToken cancellationToken) =>
        service.ApplyAsync(contentId, attemptId, request, cancellationToken);
}
