using System.Globalization;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.ReviewDetail;

public sealed record DetailStageIngredientItem(
    Guid Id,
    int SortOrder,
    string Name,
    string? Quantity,
    string IngredientType,
    bool IsPrimary,
    string? Note);

public sealed record DetailStageMediaItem(
    Guid Id,
    Guid OwnerContentId,
    string OriginalFileName,
    string ThumbnailUrl,
    string MimeType,
    long SizeBytes,
    int Width,
    int Height);

public sealed record DetailStageDto(
    Guid ContentId,
    WorkflowStep CurrentWorkflowStep,
    string RowVersion,
    string Title,
    string? ShortSummary,
    string? OriginalUrl,
    string? NormalizedUrl,
    ContentSourceKind? SourceKind,
    InstagramContentType? InstagramContentType,
    string? ManualCaption,
    PinnedAuthorCommentState? PinnedAuthorCommentState,
    string? PinnedAuthorCommentText,
    string? ManualBody,
    Guid CategoryId,
    string CategoryCode,
    string CategoryDisplayName,
    IReadOnlyDictionary<string, string?> CategoryValues,
    IReadOnlyList<DetailStageIngredientItem> Ingredients,
    IReadOnlyList<DetailStageMediaItem> LinkedMedia,
    IReadOnlyList<string> EditableFields);

public sealed record SaveDetailStageRequest(
    IReadOnlyDictionary<string, string?>? Values,
    bool Complete,
    string? RowVersion);

public interface IDetailStageRepository
{
    Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class DetailStageService(IDetailStageRepository repository)
{
    public async Task<DetailStageDto> GetAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        EnsureAvailable(content.CurrentWorkflowStep);
        DetailsConsistency.Validate(content);
        return Map(content);
    }

    public async Task<DetailStageDto> ExecuteAsync(
        Guid contentId,
        SaveDetailStageRequest request,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        if (content.CurrentWorkflowStep != WorkflowStep.DETAIL)
        {
            throw new DomainRuleException(
                IsBeforeDetail(content.CurrentWorkflowStep)
                    ? "DETAIL_STAGE_NOT_AVAILABLE"
                    : "DETAIL_STAGE_ALREADY_COMPLETED",
                IsBeforeDetail(content.CurrentWorkflowStep)
                    ? "이미지 단계를 완료한 뒤 자료 상세를 검토해 주세요."
                    : "자료 상세 단계를 이미 완료한 콘텐츠입니다.");
        }

        if (string.IsNullOrWhiteSpace(request.RowVersion))
        {
            throw new ConcurrencyConflictException("자료 상세 저장에는 rowversion이 필요합니다.");
        }

        ContentService.EnsureRowVersion(content, request.RowVersion);
        if (request.Values is { Count: > 0 })
        {
            throw new DomainRuleException(
                "DETAIL_FIELD_NOT_EDITABLE",
                "현재 자료 상세 단계에는 별도로 편집하도록 승인된 필드가 없습니다.");
        }

        DetailsConsistency.Validate(content);
        if (!request.Complete)
        {
            return Map(content);
        }

        if (string.IsNullOrWhiteSpace(content.Title))
        {
            throw new DomainRuleException(
                "DETAIL_REQUIRED_DATA_MISSING",
                "제목이 없는 콘텐츠는 자료 상세 검토를 완료할 수 없습니다.");
        }

        content.MoveTo(WorkflowStep.BLOG_DRAFT);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    private async Task<Content> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        await repository.FindAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");

    private static void EnsureAvailable(WorkflowStep step)
    {
        if (IsBeforeDetail(step))
        {
            throw new DomainRuleException(
                "DETAIL_STAGE_NOT_AVAILABLE",
                "이미지 단계를 완료한 뒤 자료 상세를 검토해 주세요.");
        }
    }

    private static bool IsBeforeDetail(WorkflowStep step) => (int)step < (int)WorkflowStep.DETAIL;

    private static DetailStageDto Map(Content content)
    {
        var category = CategoryCatalog.Get(content.CategoryId);
        var linkedMedia = content.LinkedMedia
            .Where(link => link.MediaAsset is { IsDeleted: false, StorageStatus: MediaStorageStatus.READY })
            .Select(link => link.MediaAsset)
            .OrderBy(media => media.SortOrder)
            .ThenBy(media => media.CreatedAtUtc)
            .Select(media => new DetailStageMediaItem(
                media.Id,
                media.ContentId,
                media.OriginalFileName,
                $"/api/contents/{media.ContentId}/media/{media.Id}/thumbnail",
                media.MimeType,
                media.SizeBytes,
                media.Width,
                media.Height))
            .ToArray();
        var ingredients = content.CookingDetails?.Ingredients
            .OrderBy(value => value.SortOrder)
            .Select(value => new DetailStageIngredientItem(
                value.Id,
                value.SortOrder,
                value.Name,
                value.Quantity,
                value.IngredientType,
                value.IsPrimary,
                value.Note))
            .ToArray() ?? [];

        return new DetailStageDto(
            content.Id,
            content.CurrentWorkflowStep,
            Convert.ToBase64String(content.RowVersion),
            content.Title,
            content.ShortSummary,
            content.OriginalUrl,
            content.NormalizedUrl,
            content.SourceKind,
            content.InstagramContentType,
            content.ManualCaption,
            content.PinnedAuthorCommentState,
            content.PinnedAuthorCommentText,
            content.DetailContent,
            content.CategoryId,
            category.Code,
            category.DisplayName,
            MapCurrentCategory(content, category.Code),
            ingredients,
            linkedMedia,
            []);
    }

    private static IReadOnlyDictionary<string, string?> MapCurrentCategory(Content content, string code) =>
        code switch
        {
            "PLACE" when content.PlaceDetails is { } value => Values(("address", value.Address), ("businessHours", value.BusinessHours), ("parkingInfo", value.ParkingInfo), ("recommendedMenuOrSpot", value.RecommendedMenuOrSpot)),
            "COOKING" when content.CookingDetails is { } value => Values(("servings", Number(value.Servings)), ("preparationMinutes", Number(value.PreparationMinutes)), ("cookingMinutes", Number(value.CookingMinutes)), ("difficulty", value.Difficulty)),
            "EXERCISE" when content.ExerciseDetails is { } value => Values(("targetArea", value.TargetArea), ("durationMinutes", Number(value.DurationMinutes)), ("difficulty", value.Difficulty), ("equipment", value.Equipment)),
            "CLEANING_LAUNDRY" when content.CleaningLaundryDetails is { } value => Values(("target", value.Target), ("supplies", value.Supplies), ("precautions", value.Precautions)),
            "TRAVEL" when content.TravelDetails is { } value => Values(("destination", value.Destination), ("bestSeason", value.BestSeason), ("transportation", value.Transportation), ("budgetNote", value.BudgetNote)),
            "PHOTO" when content.PhotoDetails is { } value => Values(("camera", value.Camera), ("lens", value.Lens), ("shootingSettings", value.ShootingSettings), ("location", value.Location)),
            "STUDY" when content.StudyDetails is { } value => Values(("subject", value.Subject), ("learningGoal", value.LearningGoal), ("resource", value.Resource), ("reviewCycle", value.ReviewCycle)),
            "PRODUCT" when content.ProductDetails is { } value => Values(("brand", value.Brand), ("modelName", value.ModelName), ("price", value.Price?.ToString(CultureInfo.InvariantCulture)), ("purchasePlace", value.PurchasePlace)),
            "PHONE_COMPUTER" when content.PhoneComputerDetails is { } value => Values(("deviceOrOs", value.DeviceOrOs), ("appOrProgram", value.AppOrProgram), ("problem", value.Problem), ("solution", value.Solution)),
            "TIP" when content.TipDetails is { } value => Values(("situation", value.Situation), ("keyPoint", value.KeyPoint), ("precautions", value.Precautions)),
            "OTHER" when content.OtherDetails is { } value => Values(("customLabel", value.CustomLabel), ("additionalInfo", value.AdditionalInfo)),
            _ => new Dictionary<string, string?>(StringComparer.Ordinal)
        };

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static IReadOnlyDictionary<string, string?> Values(params (string Key, string? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
}
