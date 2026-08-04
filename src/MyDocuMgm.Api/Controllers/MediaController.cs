using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;

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
    public async Task<ActionResult<MediaUploadResult>> Upload(
        Guid contentId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(
            contentId,
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);
        return CreatedAtAction(nameof(List), new { contentId }, result);
    }

    [HttpPatch("{mediaId:guid}")]
    public Task<MediaItemDto> Update(
        Guid contentId,
        Guid mediaId,
        [FromBody] UpdateMediaMetadataRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(contentId, mediaId, request, cancellationToken);

    [HttpGet("{mediaId:guid}/file")]
    public async Task<IActionResult> Original(
        Guid contentId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var file = await service.OpenOriginalAsync(contentId, mediaId, cancellationToken);
        return File(file.Content, file.MimeType, enableRangeProcessing: true);
    }

    [HttpGet("{mediaId:guid}/thumbnail")]
    public async Task<IActionResult> Thumbnail(
        Guid contentId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var file = await service.OpenThumbnailAsync(contentId, mediaId, cancellationToken);
        return File(file.Content, file.MimeType, enableRangeProcessing: true);
    }

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
    public async Task<IActionResult> Delete(
        Guid contentId,
        Guid mediaId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(contentId, mediaId, rowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPost("{mediaId:guid}/restore")]
    public Task<MediaItemDto> Restore(
        Guid contentId,
        Guid mediaId,
        [FromBody] RestoreMediaRequest request,
        CancellationToken cancellationToken) =>
        service.RestoreAsync(contentId, mediaId, request.RowVersion, cancellationToken);

    [HttpPost("{mediaId:guid}/move")]
    public async Task<IActionResult> Move(
        Guid contentId,
        Guid mediaId,
        [FromBody] MoveMediaRequest request,
        CancellationToken cancellationToken)
    {
        await service.MoveAsync(
            contentId,
            mediaId,
            request.Direction,
            request.RowVersion,
            cancellationToken);
        return NoContent();
    }
}

public sealed record ReorderMediaRequest(IReadOnlyList<Guid> MediaIds);
public sealed record RestoreMediaRequest(string RowVersion);
public sealed record MoveMediaRequest(int Direction, string RowVersion);
