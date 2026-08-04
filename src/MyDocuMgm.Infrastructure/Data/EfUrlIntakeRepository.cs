using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfUrlIntakeRepository(MyDocuMgmDbContext dbContext) : IUrlIntakeRepository
{
    public async Task<Content?> FindByNormalizedUrlAsync(
        string normalizedUrl,
        CancellationToken cancellationToken)
    {
        var hash = UrlNormalizer.ComputeHash(normalizedUrl);
        var candidate = await dbContext.Contents.IgnoreQueryFilters()
            .Include(content => content.LinkedMedia)
            .SingleOrDefaultAsync(
                content => content.NormalizedUrlHash == hash,
                cancellationToken);
        if (candidate is not null && !string.Equals(candidate.NormalizedUrl, normalizedUrl, StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                "URL_HASH_COLLISION",
                "URL 중복 키 충돌이 발생해 안전하게 저장하지 않았습니다.");
        }

        return candidate;
    }

    public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents.IgnoreQueryFilters()
            .Include(content => content.LinkedMedia)
            .SingleOrDefaultAsync(content => content.Id == contentId, cancellationToken);

    public async Task<UrlIntakeStoreResult> AddOrGetAsync(
        Content content,
        CancellationToken cancellationToken)
    {
        await dbContext.Contents.AddAsync(content, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new UrlIntakeStoreResult(content, Created: true);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = await FindByNormalizedUrlAsync(content.NormalizedUrl!, cancellationToken)
                ?? throw new InvalidOperationException("중복 URL 레코드를 다시 조회하지 못했습니다.", exception);
            return new UrlIntakeStoreResult(existing, Created: false);
        }
    }

    public async Task<LinkableMediaPage> ListLinkableMediaAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MediaAssets.AsNoTracking()
            .Where(media => media.StorageStatus == MediaStorageStatus.READY)
            .OrderByDescending(media => media.CreatedAtUtc)
            .ThenBy(media => media.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(media => new LinkableMediaItem(
                media.Id,
                media.ContentId,
                media.OriginalFileName,
                $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.SizeBytes,
                media.Width,
                media.Height))
            .ToListAsync(cancellationToken);
        return new LinkableMediaPage(items, totalCount, page, pageSize);
    }

    public async Task ReplaceLinkedMediaAsync(
        Content content,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = mediaIds.ToHashSet();
        var existingMediaIds = await dbContext.MediaAssets.AsNoTracking()
            .Where(media => requestedIds.Contains(media.Id) && media.StorageStatus == MediaStorageStatus.READY)
            .Select(media => media.Id)
            .ToListAsync(cancellationToken);
        if (existingMediaIds.Count != requestedIds.Count)
        {
            throw new DomainRuleException(
                "MEDIA_LINK_NOT_FOUND",
                "연결할 수 없는 이미지가 포함되어 있습니다.");
        }

        var removed = content.LinkedMedia
            .Where(link => !requestedIds.Contains(link.MediaAssetId))
            .ToArray();
        dbContext.ContentMediaLinks.RemoveRange(removed);
        foreach (var link in removed)
        {
            content.LinkedMedia.Remove(link);
        }
        foreach (var mediaId in requestedIds.Except(content.LinkedMedia.Select(link => link.MediaAssetId)))
        {
            content.LinkedMedia.Add(new ContentMediaLink
            {
                ContentId = content.Id,
                MediaAssetId = mediaId
            });
        }

        await SaveChangesAsync(cancellationToken);
    }

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

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
