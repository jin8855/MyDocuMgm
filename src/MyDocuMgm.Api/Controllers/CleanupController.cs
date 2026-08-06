using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/cleanup")]
public sealed class CleanupController(CleanupService service) : ControllerBase
{
    [HttpGet("trash")]
    public Task<IReadOnlyList<TrashContentItem>> Trash(CancellationToken cancellationToken) =>
        service.ListTrashAsync(cancellationToken);

    [HttpDelete("trash/{contentId:guid}")]
    public async Task<IActionResult> DeleteContent(Guid contentId, CancellationToken cancellationToken)
    {
        await service.PermanentlyDeleteContentAsync(contentId, cancellationToken);
        return NoContent();
    }

    [HttpGet("orphan-media")]
    public Task<IReadOnlyList<OrphanMediaItem>> OrphanMedia(CancellationToken cancellationToken) =>
        service.ListOrphanMediaAsync(cancellationToken);

    [HttpDelete("orphan-media/{mediaId:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid mediaId, CancellationToken cancellationToken)
    {
        await service.PermanentlyDeleteOrphanMediaAsync(mediaId, cancellationToken);
        return NoContent();
    }
}
