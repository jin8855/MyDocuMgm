using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.Contents.EditImage;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/image-stage")]
public sealed class ImageStageController(ImageStageService service) : ControllerBase
{
    [HttpGet]
    public Task<ImageStageDto> Get(Guid contentId, CancellationToken cancellationToken) =>
        service.GetAsync(contentId, cancellationToken);

    [HttpPut]
    public Task<ImageStageDto> Save(
        Guid contentId,
        [FromBody] SaveImageStageRequest request,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(contentId, request, cancellationToken);
}
