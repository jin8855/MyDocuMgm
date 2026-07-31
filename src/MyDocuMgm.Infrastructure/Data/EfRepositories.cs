using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Categories;
using MyDocuMgm.Application.CookingIngredients;
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
            .Where(content => query.WorkflowStep == null || content.CurrentWorkflowStep == query.WorkflowStep)
            .Where(content => query.IsFavorite == null || content.IsFavorite == query.IsFavorite);

        if (!string.IsNullOrWhiteSpace(query.MajorCategory))
        {
            var categoryId = CategoryCatalog.All.Single(value => value.Code == query.MajorCategory).Id;
            filtered = filtered.Where(content => content.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            filtered = query.SearchScope == SearchScope.TAG
                ? filtered.Where(content => content.ContentTags.Any(link => link.Tag.Name.Contains(keyword)))
                : filtered.Where(content =>
                    content.Title.Contains(keyword) ||
                    (content.ShortSummary != null && content.ShortSummary.Contains(keyword)) ||
                    (content.DetailContent != null && content.DetailContent.Contains(keyword)) ||
                    content.ContentTags.Any(link => link.Tag.Name.Contains(keyword)) ||
                    (content.CookingDetails != null && content.CookingDetails.Ingredients.Any(value => value.Name.Contains(keyword))) ||
                    (content.PlaceDetails != null &&
                        ((content.PlaceDetails.Address != null && content.PlaceDetails.Address.Contains(keyword)) ||
                         (content.PlaceDetails.ParkingInfo != null && content.PlaceDetails.ParkingInfo.Contains(keyword)))) ||
                    (content.ProductDetails != null &&
                        ((content.ProductDetails.Brand != null && content.ProductDetails.Brand.Contains(keyword)) ||
                         (content.ProductDetails.PurchasePlace != null && content.ProductDetails.PurchasePlace.Contains(keyword)))) ||
                    (content.PhoneComputerDetails != null &&
                        ((content.PhoneComputerDetails.DeviceOrOs != null && content.PhoneComputerDetails.DeviceOrOs.Contains(keyword)) ||
                         (content.PhoneComputerDetails.Problem != null && content.PhoneComputerDetails.Problem.Contains(keyword)))));
        }

        if (!string.IsNullOrWhiteSpace(query.AttributeKey) && !string.IsNullOrWhiteSpace(query.AttributeValue))
        {
            filtered = ApplyAttributeFilter(filtered, query.AttributeKey, query.AttributeValue.Trim());
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize;
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
                content.CurrentWorkflowStep,
                content.CurrentWorkflowStep == WorkflowStep.BLOG_DRAFT || content.CurrentWorkflowStep == WorkflowStep.COMPLETED
                    ? "초안 준비"
                    : "미작성",
                content.UpdatedAtUtc,
                Convert.ToBase64String(content.RowVersion),
                content.ContentTags.Select(link => link.Tag.Name).OrderBy(value => value).ToArray()))
            .ToListAsync(cancellationToken);
        return new PagedResult<ContentSummary>(items, total, page, pageSize);
    }

    private static IQueryable<Content> ApplyAttributeFilter(
        IQueryable<Content> query,
        string attributeKey,
        string value) =>
        attributeKey switch
        {
            "primaryIngredient" => query.Where(content =>
                content.CookingDetails != null &&
                content.CookingDetails.Ingredients.Any(ingredient => ingredient.IsPrimary && ingredient.Name.Contains(value))),
            "difficulty" => query.Where(content =>
                content.CookingDetails != null && content.CookingDetails.Difficulty != null &&
                content.CookingDetails.Difficulty.Contains(value)),
            "time" => int.TryParse(value, out var minutes)
                ? query.Where(content => content.CookingDetails != null &&
                    (content.CookingDetails.PreparationMinutes ?? 0) + (content.CookingDetails.CookingMinutes ?? 0) == minutes)
                : query.Where(_ => false),
            "brand" => query.Where(content =>
                content.ProductDetails != null && content.ProductDetails.Brand != null &&
                content.ProductDetails.Brand.Contains(value)),
            "store" => query.Where(content =>
                content.ProductDetails != null && content.ProductDetails.PurchasePlace != null &&
                content.ProductDetails.PurchasePlace.Contains(value)),
            "price" => decimal.TryParse(value, out var price)
                ? query.Where(content => content.ProductDetails != null && content.ProductDetails.Price == price)
                : query.Where(_ => false),
            "region" => query.Where(content =>
                content.PlaceDetails != null && content.PlaceDetails.Address != null &&
                content.PlaceDetails.Address.Contains(value)),
            "parking" => query.Where(content =>
                content.PlaceDetails != null && content.PlaceDetails.ParkingInfo != null &&
                content.PlaceDetails.ParkingInfo.Contains(value)),
            "destination" => query.Where(content =>
                content.TravelDetails != null && content.TravelDetails.Destination != null &&
                content.TravelDetails.Destination.Contains(value)),
            "transport" => query.Where(content =>
                content.TravelDetails != null && content.TravelDetails.Transportation != null &&
                content.TravelDetails.Transportation.Contains(value)),
            "targetArea" => query.Where(content =>
                content.ExerciseDetails != null && content.ExerciseDetails.TargetArea != null &&
                content.ExerciseDetails.TargetArea.Contains(value)),
            "equipment" => query.Where(content =>
                content.ExerciseDetails != null && content.ExerciseDetails.Equipment != null &&
                content.ExerciseDetails.Equipment.Contains(value)),
            "target" => query.Where(content =>
                content.CleaningLaundryDetails != null && content.CleaningLaundryDetails.Target != null &&
                content.CleaningLaundryDetails.Target.Contains(value)),
            "supplies" => query.Where(content =>
                content.CleaningLaundryDetails != null && content.CleaningLaundryDetails.Supplies != null &&
                content.CleaningLaundryDetails.Supplies.Contains(value)),
            "camera" => query.Where(content =>
                content.PhotoDetails != null && content.PhotoDetails.Camera != null &&
                content.PhotoDetails.Camera.Contains(value)),
            "location" => query.Where(content =>
                content.PhotoDetails != null && content.PhotoDetails.Location != null &&
                content.PhotoDetails.Location.Contains(value)),
            "subject" => query.Where(content =>
                content.StudyDetails != null && content.StudyDetails.Subject != null &&
                content.StudyDetails.Subject.Contains(value)),
            "resource" => query.Where(content =>
                content.StudyDetails != null && content.StudyDetails.Resource != null &&
                content.StudyDetails.Resource.Contains(value)),
            "deviceOrOs" => query.Where(content =>
                content.PhoneComputerDetails != null && content.PhoneComputerDetails.DeviceOrOs != null &&
                content.PhoneComputerDetails.DeviceOrOs.Contains(value)),
            "problem" => query.Where(content =>
                content.PhoneComputerDetails != null && content.PhoneComputerDetails.Problem != null &&
                content.PhoneComputerDetails.Problem.Contains(value)),
            "situation" => query.Where(content =>
                content.TipDetails != null && content.TipDetails.Situation != null &&
                content.TipDetails.Situation.Contains(value)),
            "keyPoint" => query.Where(content =>
                content.TipDetails != null && content.TipDetails.KeyPoint != null &&
                content.TipDetails.KeyPoint.Contains(value)),
            "customLabel" => query.Where(content =>
                content.OtherDetails != null && content.OtherDetails.CustomLabel != null &&
                content.OtherDetails.CustomLabel.Contains(value)),
            _ => throw new DomainRuleException("INVALID_ATTRIBUTE_KEY", "허용되지 않은 검색 속성입니다.")
        };

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

    public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) =>
        dbContext.Tags.AddAsync(tag, cancellationToken).AsTask();

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

    public async Task<MediaPage> SearchAsync(MediaQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.MediaAssets.AsNoTracking().Where(media => media.ContentId == query.ContentId);
        var duplicateHashes = dbContext.MediaAssets.AsNoTracking()
            .Where(media => media.ContentId == query.ContentId)
            .GroupBy(media => media.Sha256)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        var selectedCount = await baseQuery.CountAsync(media => media.IsSelected, cancellationToken);
        var duplicateCount = await baseQuery.CountAsync(media => duplicateHashes.Contains(media.Sha256), cancellationToken);
        var filtered = query.Filter switch
        {
            MediaFilter.SELECTED => baseQuery.Where(media => media.IsSelected),
            MediaFilter.DUPLICATE => baseQuery.Where(media => duplicateHashes.Contains(media.Sha256)),
            _ => baseQuery
        };
        var filteredCount = await filtered.CountAsync(cancellationToken);
        var ordered = query.Sort == MediaSort.TIME_DESC
            ? filtered.OrderBy(media => media.SourceTimestampMs == null)
                .ThenByDescending(media => media.SourceTimestampMs)
                .ThenByDescending(media => media.SortOrder)
            : filtered.OrderBy(media => media.SourceTimestampMs == null)
                .ThenBy(media => media.SourceTimestampMs)
                .ThenBy(media => media.SortOrder);
        var items = await ordered.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(media => new MediaItemDto(
                media.Id,
                media.OriginalFileName,
                $"/api/contents/{query.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.SizeBytes,
                media.Width,
                media.Height,
                media.SortOrder,
                media.SourceTimestampMs,
                media.IsSelected,
                media.IsPublicAllowed,
                media.Description,
                media.StorageStatus,
                media.Sha256,
                Convert.ToBase64String(media.RowVersion)))
            .ToListAsync(cancellationToken);
        return new MediaPage(items, filteredCount, selectedCount, duplicateCount, query.Page, query.PageSize);
    }
}

public sealed class EfCookingIngredientRepository(MyDocuMgmDbContext dbContext) : ICookingIngredientRepository
{
    public Task<Content?> FindCookingContentAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents.Include(content => content.CookingDetails)
            .ThenInclude(details => details!.Ingredients)
            .SingleOrDefaultAsync(content => content.Id == contentId, cancellationToken);

    public Task<CookingIngredient?> FindAsync(Guid contentId, Guid ingredientId, CancellationToken cancellationToken) =>
        dbContext.CookingIngredients.SingleOrDefaultAsync(
            ingredient => ingredient.ContentId == contentId && ingredient.Id == ingredientId,
            cancellationToken);

    public async Task<IReadOnlyList<CookingIngredient>> ListAsync(Guid contentId, CancellationToken cancellationToken) =>
        await dbContext.CookingIngredients.Where(ingredient => ingredient.ContentId == contentId)
            .OrderBy(ingredient => ingredient.SortOrder)
            .ToListAsync(cancellationToken);

    public Task AddAsync(CookingIngredient ingredient, CancellationToken cancellationToken) =>
        dbContext.CookingIngredients.AddAsync(ingredient, cancellationToken).AsTask();

    public void Remove(CookingIngredient ingredient) => dbContext.CookingIngredients.Remove(ingredient);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException($"다른 재료 변경이 먼저 저장되었습니다: {exception.Message}");
        }
    }
}

public sealed class EfCategoryManagementRepository(MyDocuMgmDbContext dbContext) : ICategoryManagementRepository
{
    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.OrderBy(category => category.SortOrder).ToListAsync(cancellationToken);

    public Task<Category?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Categories.SingleOrDefaultAsync(category => category.Code == code, cancellationToken);

    public async Task<IReadOnlyList<CategorySearchAttribute>> ListAttributesAsync(
        string categoryCode,
        CancellationToken cancellationToken) =>
        await dbContext.CategorySearchAttributes
            .Where(attribute => attribute.Category.Code == categoryCode)
            .OrderBy(attribute => attribute.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<CategorySearchAttribute?> FindAttributeAsync(
        string categoryCode,
        string attributeKey,
        CancellationToken cancellationToken) =>
        dbContext.CategorySearchAttributes.SingleOrDefaultAsync(
            attribute => attribute.Category.Code == categoryCode && attribute.AttributeKey == attributeKey,
            cancellationToken);

    public Task<bool> IsCategoryInUseAsync(Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.Contents.IgnoreQueryFilters().AnyAsync(content => content.CategoryId == categoryId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException($"다른 분류 변경이 먼저 저장되었습니다: {exception.Message}");
        }
    }
}
