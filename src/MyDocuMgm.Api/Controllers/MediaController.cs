using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/media")]
public sealed class MediaController(MediaService service) : ControllerBase
{
    [HttpGet]
    public Task<MediaPage> List(
        Guid contentId,
        [FromQuery] MediaFilter filter = MediaFilter.ALL,
        [FromQuery] MediaSort sort = MediaSort.TIME_ASC,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(new MediaQuery(contentId, filter, sort, page, pageSize), cancellationToken);

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
        [FromBody] UpdateMediaMetadataRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(contentId, mediaId, request, cancellationToken);

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
public sealed record ReorderMediaRequest(IReadOnlyList<Guid> MediaIds);
