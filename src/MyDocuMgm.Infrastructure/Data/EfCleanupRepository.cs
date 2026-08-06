using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfCleanupRepository(MyDocuMgmDbContext dbContext) : ICleanupRepository
{
    public async Task<IReadOnlyList<TrashContentItem>> ListTrashAsync(CancellationToken cancellationToken) =>
        await dbContext.Contents.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(content => content.IsDeleted)
            .OrderByDescending(content => content.DeletedAtUtc)
            .Select(content => new TrashContentItem(
                content.Id,
                content.Title,
                content.DeletedAtUtc,
                content.MediaAssets.Count,
                Convert.ToBase64String(content.RowVersion)))
            .ToListAsync(cancellationToken);

    public Task<Content?> FindContentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Contents.IgnoreQueryFilters()
            .SingleOrDefaultAsync(content => content.Id == id, cancellationToken);

    public Task<int> CountOwnedMediaAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.MediaAssets.IgnoreQueryFilters()
            .CountAsync(media => media.ContentId == contentId, cancellationToken);

    public void RemoveContent(Content content) => dbContext.Contents.Remove(content);

    public async Task<IReadOnlyList<CleanupMediaCandidate>> ListOrphanMediaAsync(
        CancellationToken cancellationToken) =>
        await dbContext.MediaAssets.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(media => !media.LinkedContents.Any())
            .OrderBy(media => media.CreatedAtUtc)
            .Select(media => new CleanupMediaCandidate(
                media,
                media.LinkedContents.Count,
                media.ContentSteps.Count))
            .ToListAsync(cancellationToken);

    public Task<CleanupMediaCandidate?> FindMediaAsync(Guid mediaId, CancellationToken cancellationToken) =>
        dbContext.MediaAssets.IgnoreQueryFilters()
            .Where(media => media.Id == mediaId)
            .Select(media => new CleanupMediaCandidate(
                media,
                media.LinkedContents.Count,
                media.ContentSteps.Count))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountMediaPathReferencesAsync(
        string relativePath,
        Guid excludingMediaId,
        CancellationToken cancellationToken) =>
        dbContext.MediaAssets.IgnoreQueryFilters()
            .CountAsync(
                media => media.Id != excludingMediaId && media.RelativePath == relativePath,
                cancellationToken);

    public void RemoveMedia(MediaAsset media) => dbContext.MediaAssets.Remove(media);

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
            throw new CleanupConflictException(
                "CLEANUP_REFERENCE_CONFLICT",
                $"삭제 직전에 새로운 참조가 확인되어 작업을 중단했습니다: {exception.GetType().Name}");
        }
    }
}
