using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfContentRepository(MyDocuMgmDbContext dbContext) : IContentRepository
{
    public async Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken)
    {
        var contents = query.IncludeDeleted ? dbContext.Contents.IgnoreQueryFilters() : dbContext.Contents;
        var filtered = contents.AsNoTracking()
            .Where(content => query.IncludeDeleted || !content.IsDeleted)
            .Where(content => query.CategoryId == null || content.CategoryId == query.CategoryId)
            .Where(content => query.Status == null || content.Status == query.Status)
            .Where(content => query.IsFavorite == null || content.IsFavorite == query.IsFavorite)
            .Where(content => string.IsNullOrWhiteSpace(query.Search) ||
                content.Title.Contains(query.Search) ||
                (content.ShortSummary != null && content.ShortSummary.Contains(query.Search)));
        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await filtered.OrderByDescending(content => content.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(content => new ContentSummary(
                content.Id,
                content.Category.Code,
                content.Category.DisplayName,
                content.Title,
                content.ShortSummary,
                content.Status,
                content.Visibility,
                content.IsFavorite,
                content.UpdatedAtUtc,
                Convert.ToBase64String(content.RowVersion)))
            .ToListAsync(cancellationToken);
        return new PagedResult<ContentSummary>(items, total, page, pageSize);
    }

    public Task<Content?> FindAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = includeDeleted ? dbContext.Contents.IgnoreQueryFilters() : dbContext.Contents;
        return query.Include(content => content.ContentTags).ThenInclude(link => link.Tag)
            .Include(content => content.PlaceDetails)
            .Include(content => content.CookingDetails).ThenInclude(details => details!.Ingredients)
            .Include(content => content.ExerciseDetails)
            .Include(content => content.CleaningLaundryDetails)
            .Include(content => content.TravelDetails)
            .Include(content => content.PhotoDetails)
            .Include(content => content.StudyDetails)
            .Include(content => content.ProductDetails)
            .Include(content => content.PhoneComputerDetails)
            .Include(content => content.TipDetails)
            .Include(content => content.OtherDetails)
            .SingleOrDefaultAsync(content => content.Id == id, cancellationToken);
    }

    public Task AddAsync(Content content, CancellationToken cancellationToken) =>
        dbContext.Contents.AddAsync(content, cancellationToken).AsTask();

    public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) =>
        dbContext.Tags.SingleOrDefaultAsync(tag => tag.NormalizedName == normalizedName, cancellationToken);

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

public sealed class EfMediaAssetRepository(MyDocuMgmDbContext dbContext) : IMediaAssetRepository
{
    public Task<MediaAsset?> FindAsync(Guid contentId, Guid mediaId, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = includeDeleted ? dbContext.MediaAssets.IgnoreQueryFilters() : dbContext.MediaAssets;
        return query.SingleOrDefaultAsync(media => media.ContentId == contentId && media.Id == mediaId, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaAsset>> ListAsync(
        Guid contentId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = includeDeleted ? dbContext.MediaAssets.IgnoreQueryFilters() : dbContext.MediaAssets;
        return await query.Where(media => media.ContentId == contentId)
            .OrderBy(media => media.SortOrder)
            .ThenBy(media => media.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(MediaAsset media, CancellationToken cancellationToken) =>
        dbContext.MediaAssets.AddAsync(media, cancellationToken).AsTask();

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
