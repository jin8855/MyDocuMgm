using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<CategoryDefinition>> Get() => Ok(CategoryCatalog.All);
}
