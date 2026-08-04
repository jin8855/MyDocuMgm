using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.UrlIntake;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/url-intakes")]
public sealed class UrlIntakeController(UrlIntakeService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UrlIntakeDto>> Create(
        [FromBody] CreateUrlIntakeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.IntakeAsync(request, cancellationToken);
        return result.IsDuplicate
            ? Ok(result)
            : CreatedAtAction(nameof(Get), new { contentId = result.Id }, result);
    }

    [HttpGet("{contentId:guid}")]
    public Task<UrlIntakeDto> Get(Guid contentId, CancellationToken cancellationToken) =>
        service.GetAsync(contentId, cancellationToken);

    [HttpPost("{contentId:guid}/manual-input")]
    public Task<UrlIntakeDto> BeginManualInput(
        Guid contentId,
        CancellationToken cancellationToken) =>
        service.BeginManualInputAsync(contentId, cancellationToken);

    [HttpPut("{contentId:guid}/manual-body")]
    public Task<UrlIntakeDto> SaveManualBody(
        Guid contentId,
        [FromBody] SaveManualBodyRequest request,
        CancellationToken cancellationToken) =>
        service.SaveManualBodyAsync(contentId, request, cancellationToken);

    [HttpGet("media-library")]
    public Task<LinkableMediaPage> MediaLibrary(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default) =>
        service.ListLinkableMediaAsync(page, pageSize, cancellationToken);

    [HttpPut("{contentId:guid}/media-links")]
    public Task<UrlIntakeDto> ReplaceMediaLinks(
        Guid contentId,
        [FromBody] ReplaceLinkedMediaRequest request,
        CancellationToken cancellationToken) =>
        service.ReplaceLinkedMediaAsync(contentId, request, cancellationToken);
}
