using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.EditImage;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfImageStageRepository(MyDocuMgmDbContext dbContext) : IImageStageRepository
{
    public async Task<ImageStageData?> FindAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await dbContext.Contents
            .Include(value => value.LinkedMedia)
            .SingleOrDefaultAsync(value => value.Id == contentId, cancellationToken);
        if (content is null)
        {
            return null;
        }

        var existingLinkedIds = content.LinkedMedia.Select(link => link.MediaAssetId).ToArray();
        var availableMedia = await dbContext.MediaAssets.AsNoTracking()
            .Where(media =>
                media.StorageStatus == MediaStorageStatus.READY &&
                (media.ContentId == contentId || existingLinkedIds.Contains(media.Id)))
            .OrderBy(media => media.SortOrder)
            .ThenBy(media => media.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return new ImageStageData(content, availableMedia);
    }

    public void RemoveLinks(IReadOnlyCollection<ContentMediaLink> links) =>
        dbContext.ContentMediaLinks.RemoveRange(links);

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
    }
}
