using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.Categories;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryManagementService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List(CancellationToken cancellationToken) =>
        service.ListAsync(cancellationToken);

    [HttpPatch("{code}")]
    public Task<CategoryDto> Update(
        string code,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(code, request, cancellationToken);

    [HttpGet("{code}/search-attributes")]
    public Task<IReadOnlyList<CategorySearchAttributeDto>> ListSearchAttributes(
        string code,
        CancellationToken cancellationToken) =>
        service.ListAttributesAsync(code, cancellationToken);

    [HttpPatch("{code}/search-attributes/{attributeKey}")]
    public Task<CategorySearchAttributeDto> UpdateSearchAttribute(
        string code,
        string attributeKey,
        [FromBody] UpdateSearchAttributeRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAttributeAsync(code, attributeKey, request, cancellationToken);
}
