using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.Contents.EditCategory;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/category-edit")]
public sealed class CategoryEditController(CategoryEditService service) : ControllerBase
{
    [HttpGet]
    public Task<CategoryEditDto> Get(Guid contentId, CancellationToken cancellationToken) =>
        service.GetAsync(contentId, cancellationToken);

    [HttpPut]
    public Task<CategoryEditDto> Save(
        Guid contentId,
        [FromBody] SaveCategoryEditRequest request,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(contentId, request, cancellationToken);
}
