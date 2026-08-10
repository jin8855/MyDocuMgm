using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.Contents.ReviewDetail;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/detail-stage")]
public sealed class DetailStageController(DetailStageService service) : ControllerBase
{
    [HttpGet]
    public Task<DetailStageDto> Get(Guid contentId, CancellationToken cancellationToken) =>
        service.GetAsync(contentId, cancellationToken);

    [HttpPut]
    public Task<DetailStageDto> Save(
        Guid contentId,
        [FromBody] SaveDetailStageRequest request,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(contentId, request, cancellationToken);
}
