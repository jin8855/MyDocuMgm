using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application.Contents.WriteBlogDraft;

namespace MyDocuMgm.Api.Controllers;

[ApiController]
[Route("api/contents/{contentId:guid}/blog-draft")]
public sealed class BlogDraftController(BlogDraftService service) : ControllerBase
{
    [HttpGet]
    public Task<BlogDraftDto> Get(Guid contentId, CancellationToken cancellationToken) =>
        service.GetAsync(contentId, cancellationToken);

    [HttpGet("~/api/contents/{contentId:guid}/completion")]
    public Task<BlogDraftDto> GetCompletion(Guid contentId, CancellationToken cancellationToken) =>
        service.GetCompletionAsync(contentId, cancellationToken);

    [HttpPut]
    public Task<BlogDraftDto> Save(
        Guid contentId,
        [FromBody] SaveBlogDraftRequest request,
        CancellationToken cancellationToken) =>
        service.SaveAsync(contentId, request, cancellationToken);
}
