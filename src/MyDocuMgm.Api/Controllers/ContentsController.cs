using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;
using MyDocuMgm.Application.Contents.UpdateWorkflowStep;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents")]
public sealed class ContentsController(ContentService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ContentSummary>> List(
        [FromQuery] string? keyword = null,
        [FromQuery] string? majorCategory = null,
        [FromQuery] string? attributeKey = null,
        [FromQuery] string? attributeValue = null,
        [FromQuery] SearchScope searchScope = SearchScope.ALL,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] ContentStatus? status = null,
        [FromQuery] WorkflowStep? workflowStep = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(
            new ContentQuery(
                keyword,
                majorCategory,
                attributeKey,
                attributeValue,
                searchScope,
                categoryId,
                status,
                workflowStep,
                isFavorite,
                includeDeleted,
                page,
                pageSize),
            cancellationToken);

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

[ApiController]
[Route("api/contents/{contentId:guid}/workflow")]
public sealed class ContentWorkflowController(UpdateWorkflowStepService service) : ControllerBase
{
    [HttpPatch]
    public Task<ContentDetail> Update(
        Guid contentId,
        [FromBody] UpdateWorkflowStepRequest request,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(contentId, request, cancellationToken);
}
