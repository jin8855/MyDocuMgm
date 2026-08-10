using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.WriteBlogDraft;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfBlogDraftRepository(MyDocuMgmDbContext dbContext) : IBlogDraftRepository
{
    public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents
            .Include(content => content.BlogDraft)
            .Include(content => content.LinkedMedia)
            .ThenInclude(link => link.MediaAsset)
            .SingleOrDefaultAsync(content => content.Id == contentId, cancellationToken);

    public void Add(BlogDraft draft) => dbContext.BlogDrafts.Add(draft);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException($"다른 변경이 먼저 저장되었습니다: {exception.Message}");
        }
        catch (DbUpdateException exception)
        {
            throw new ConcurrencyConflictException($"블로그 초안 저장이 다른 요청과 충돌했습니다: {exception.Message}");
        }
    }
}
