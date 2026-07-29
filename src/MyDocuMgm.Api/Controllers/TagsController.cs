using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Infrastructure.Data;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(MyDocuMgmDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public Task<string[]> Get([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Tags.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(tag => tag.Name.Contains(search));
        }

        return query.OrderBy(tag => tag.Name).Select(tag => tag.Name).Take(50).ToArrayAsync(cancellationToken);
    }
}
