using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/media")]
public sealed class MediaController(MediaService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MediaAsset>> List(
        Guid contentId,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken) =>
        service.ListAsync(contentId, includeDeleted, cancellationToken);

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024 + 65_536)]
    public async Task<ActionResult<MediaAsset>> Upload(
        Guid contentId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var media = await service.UploadAsync(contentId, stream, file.FileName, file.ContentType, cancellationToken);
        return CreatedAtAction(nameof(List), new { contentId }, media);
    }

    [HttpPatch("{mediaId:guid}")]
    public Task<MediaAsset> Update(
        Guid contentId,
        Guid mediaId,
        [FromBody] UpdateMediaRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(contentId, mediaId, request.Description, request.IsPublicAllowed, cancellationToken);

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(
        Guid contentId,
        [FromBody] ReorderMediaRequest request,
        CancellationToken cancellationToken)
    {
        await service.ReorderAsync(contentId, request.MediaIds, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{mediaId:guid}")]
    public async Task<IActionResult> Delete(Guid contentId, Guid mediaId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(contentId, mediaId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{mediaId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid contentId, Guid mediaId, CancellationToken cancellationToken)
    {
        await service.RestoreAsync(contentId, mediaId, cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateMediaRequest(string? Description, bool IsPublicAllowed);
public sealed record ReorderMediaRequest(IReadOnlyList<Guid> MediaIds);
