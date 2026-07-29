using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents")]
public sealed class ContentsController(ContentService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ContentSummary>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] ContentStatus? status,
        [FromQuery] bool? isFavorite,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(new ContentQuery(search, categoryId, status, isFavorite, includeDeleted, page, pageSize), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ContentDetail>> Create(
        [FromBody] SaveContentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public Task<ContentDetail> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<ContentDetail> Update(
        Guid id,
        [FromBody] SaveContentRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, rowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public Task<ContentDetail> Restore(Guid id, CancellationToken cancellationToken) =>
        service.RestoreAsync(id, cancellationToken);
}
