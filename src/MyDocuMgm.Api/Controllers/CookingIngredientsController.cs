using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.CookingIngredients;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/ingredients")]
public sealed class CookingIngredientsController(CookingIngredientService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CookingIngredientDto>> List(Guid contentId, CancellationToken cancellationToken) =>
        service.ListAsync(contentId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CookingIngredientDto>> Add(
        Guid contentId,
        [FromBody] SaveCookingIngredientRequest request,
        CancellationToken cancellationToken)
    {
        var created = await service.AddAsync(contentId, request, cancellationToken);
        return Created($"/api/contents/{contentId}/ingredients/{created.Id}", created);
    }

    [HttpPut("{ingredientId:guid}")]
    public Task<CookingIngredientDto> Update(
        Guid contentId,
        Guid ingredientId,
        [FromBody] SaveCookingIngredientRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(contentId, ingredientId, request, cancellationToken);

    [HttpDelete("{ingredientId:guid}")]
    public async Task<IActionResult> Delete(
        Guid contentId,
        Guid ingredientId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(contentId, ingredientId, rowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(
        Guid contentId,
        [FromBody] ReorderCookingIngredientsRequest request,
        CancellationToken cancellationToken)
    {
        await service.ReorderAsync(contentId, request, cancellationToken);
        return NoContent();
    }
}
