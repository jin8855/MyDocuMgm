using System.Globalization;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.EditCategory;

public sealed record CategoryEditDto(
    Guid ContentId,
    string Title,
    string? ShortSummary,
    Guid CategoryId,
    string CategoryCode,
    WorkflowStep CurrentWorkflowStep,
    string RowVersion,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> ValuesByCategory);

public sealed record SaveCategoryEditRequest(
    Guid CategoryId,
    IReadOnlyDictionary<string, string?>? Values,
    bool Complete,
    string? RowVersion);

public sealed class CategoryEditService(IContentRepository repository)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFields =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["PLACE"] = Fields("address", "businessHours", "parkingInfo", "recommendedMenuOrSpot"),
            ["COOKING"] = Fields("servings", "preparationMinutes", "cookingMinutes", "difficulty"),
            ["EXERCISE"] = Fields("targetArea", "durationMinutes", "difficulty", "equipment"),
            ["CLEANING_LAUNDRY"] = Fields("target", "supplies", "precautions"),
            ["TRAVEL"] = Fields("destination", "bestSeason", "transportation", "budgetNote"),
            ["PHOTO"] = Fields("camera", "lens", "shootingSettings", "location"),
            ["STUDY"] = Fields("subject", "learningGoal", "resource", "reviewCycle"),
            ["PRODUCT"] = Fields("brand", "modelName", "price", "purchasePlace"),
            ["PHONE_COMPUTER"] = Fields("deviceOrOs", "appOrProgram", "problem", "solution"),
            ["TIP"] = Fields("situation", "keyPoint", "precautions"),
            ["OTHER"] = Fields("customLabel", "additionalInfo")
        };

    public async Task<CategoryEditDto> GetAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await FindAvailableAsync(contentId, cancellationToken);
        return Map(content);
    }

    public async Task<CategoryEditDto> ExecuteAsync(
        Guid contentId,
        SaveCategoryEditRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CategoryId == Guid.Empty)
        {
            throw new DomainRuleException("CATEGORY_REQUIRED", "분류를 선택해 주세요.");
        }

        var category = CategoryCatalog.Get(request.CategoryId);
        var values = request.Values ?? new Dictionary<string, string?>();
        ValidateFields(category.Code, values);

        var content = await repository.FindAsync(contentId, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        if (content.CurrentWorkflowStep != WorkflowStep.CATEGORY_EDIT)
        {
            throw new DomainRuleException(
                IsBeforeCategoryEdit(content.CurrentWorkflowStep)
                    ? "CATEGORY_EDIT_NOT_AVAILABLE"
                    : "CATEGORY_EDIT_ALREADY_COMPLETED",
                IsBeforeCategoryEdit(content.CurrentWorkflowStep)
                    ? "분석 검토를 완료한 뒤 분류별 편집을 진행해 주세요."
                    : "분류별 편집을 이미 완료한 콘텐츠입니다.");
        }

        if (string.IsNullOrWhiteSpace(request.RowVersion))
        {
            throw new ConcurrencyConflictException("분류별 편집에는 rowversion이 필요합니다.");
        }

        ContentService.EnsureRowVersion(content, request.RowVersion);
        content.ChangeCategory(category.Id);
        Apply(content, category.Code, values);
        if (request.Complete)
        {
            content.MoveTo(WorkflowStep.MEDIA);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    private async Task<Content> FindAvailableAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(contentId, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        if (IsBeforeCategoryEdit(content.CurrentWorkflowStep))
        {
            throw new DomainRuleException(
                "CATEGORY_EDIT_NOT_AVAILABLE",
                "분석 검토를 완료한 뒤 분류별 편집을 진행해 주세요.");
        }

        return content;
    }

    private static bool IsBeforeCategoryEdit(WorkflowStep step) =>
        Array.IndexOf(WorkflowStepRules.All.ToArray(), step) <
        Array.IndexOf(WorkflowStepRules.All.ToArray(), WorkflowStep.CATEGORY_EDIT);

    private static void ValidateFields(string categoryCode, IReadOnlyDictionary<string, string?> values)
    {
        var allowed = AllowedFields[categoryCode];
        if (values.Keys.Any(key => !allowed.Contains(key)))
        {
            throw new DomainRuleException("CATEGORY_FIELD_NOT_ALLOWED", "선택한 분류에서 편집할 수 없는 항목이 포함되어 있습니다.");
        }

        foreach (var (key, value) in values)
        {
            var maxLength = key is "solution" or "additionalInfo" ? 4000 : 500;
            if (value?.Length > maxLength)
            {
                throw new DomainRuleException("CATEGORY_FIELD_TOO_LONG", $"{key} 값이 허용 길이를 초과했습니다.");
            }
        }

        ValidatePositiveInt(values, "servings", minimum: 1);
        ValidatePositiveInt(values, "preparationMinutes", minimum: 0);
        ValidatePositiveInt(values, "cookingMinutes", minimum: 0);
        ValidatePositiveInt(values, "durationMinutes", minimum: 0);
        if (values.TryGetValue("price", out var price) && !string.IsNullOrWhiteSpace(price) &&
            (!decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0))
        {
            throw new DomainRuleException("CATEGORY_FIELD_INVALID_NUMBER", "가격은 0 이상의 숫자로 입력해 주세요.");
        }
    }

    private static void ValidatePositiveInt(IReadOnlyDictionary<string, string?> values, string key, int minimum)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) &&
            (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum))
        {
            throw new DomainRuleException("CATEGORY_FIELD_INVALID_NUMBER", $"{key} 값이 올바르지 않습니다.");
        }
    }

    private static void Apply(Content content, string code, IReadOnlyDictionary<string, string?> values)
    {
        switch (code)
        {
            case "PLACE":
                content.PlaceDetails ??= New<PlaceDetails>(content);
                ApplyText(values, "address", value => content.PlaceDetails.Address = value);
                ApplyText(values, "businessHours", value => content.PlaceDetails.BusinessHours = value);
                ApplyText(values, "parkingInfo", value => content.PlaceDetails.ParkingInfo = value);
                ApplyText(values, "recommendedMenuOrSpot", value => content.PlaceDetails.RecommendedMenuOrSpot = value);
                break;
            case "COOKING":
                content.CookingDetails ??= New<CookingDetails>(content);
                ApplyInt(values, "servings", value => content.CookingDetails.Servings = value);
                ApplyInt(values, "preparationMinutes", value => content.CookingDetails.PreparationMinutes = value);
                ApplyInt(values, "cookingMinutes", value => content.CookingDetails.CookingMinutes = value);
                ApplyText(values, "difficulty", value => content.CookingDetails.Difficulty = value);
                break;
            case "EXERCISE":
                content.ExerciseDetails ??= New<ExerciseDetails>(content);
                ApplyText(values, "targetArea", value => content.ExerciseDetails.TargetArea = value);
                ApplyInt(values, "durationMinutes", value => content.ExerciseDetails.DurationMinutes = value);
                ApplyText(values, "difficulty", value => content.ExerciseDetails.Difficulty = value);
                ApplyText(values, "equipment", value => content.ExerciseDetails.Equipment = value);
                break;
            case "CLEANING_LAUNDRY":
                content.CleaningLaundryDetails ??= New<CleaningLaundryDetails>(content);
                ApplyText(values, "target", value => content.CleaningLaundryDetails.Target = value);
                ApplyText(values, "supplies", value => content.CleaningLaundryDetails.Supplies = value);
                ApplyText(values, "precautions", value => content.CleaningLaundryDetails.Precautions = value);
                break;
            case "TRAVEL":
                content.TravelDetails ??= New<TravelDetails>(content);
                ApplyText(values, "destination", value => content.TravelDetails.Destination = value);
                ApplyText(values, "bestSeason", value => content.TravelDetails.BestSeason = value);
                ApplyText(values, "transportation", value => content.TravelDetails.Transportation = value);
                ApplyText(values, "budgetNote", value => content.TravelDetails.BudgetNote = value);
                break;
            case "PHOTO":
                content.PhotoDetails ??= New<PhotoDetails>(content);
                ApplyText(values, "camera", value => content.PhotoDetails.Camera = value);
                ApplyText(values, "lens", value => content.PhotoDetails.Lens = value);
                ApplyText(values, "shootingSettings", value => content.PhotoDetails.ShootingSettings = value);
                ApplyText(values, "location", value => content.PhotoDetails.Location = value);
                break;
            case "STUDY":
                content.StudyDetails ??= New<StudyDetails>(content);
                ApplyText(values, "subject", value => content.StudyDetails.Subject = value);
                ApplyText(values, "learningGoal", value => content.StudyDetails.LearningGoal = value);
                ApplyText(values, "resource", value => content.StudyDetails.Resource = value);
                ApplyText(values, "reviewCycle", value => content.StudyDetails.ReviewCycle = value);
                break;
            case "PRODUCT":
                content.ProductDetails ??= New<ProductDetails>(content);
                ApplyText(values, "brand", value => content.ProductDetails.Brand = value);
                ApplyText(values, "modelName", value => content.ProductDetails.ModelName = value);
                ApplyDecimal(values, "price", value => content.ProductDetails.Price = value);
                ApplyText(values, "purchasePlace", value => content.ProductDetails.PurchasePlace = value);
                break;
            case "PHONE_COMPUTER":
                content.PhoneComputerDetails ??= New<PhoneComputerDetails>(content);
                ApplyText(values, "deviceOrOs", value => content.PhoneComputerDetails.DeviceOrOs = value);
                ApplyText(values, "appOrProgram", value => content.PhoneComputerDetails.AppOrProgram = value);
                ApplyText(values, "problem", value => content.PhoneComputerDetails.Problem = value);
                ApplyText(values, "solution", value => content.PhoneComputerDetails.Solution = value);
                break;
            case "TIP":
                content.TipDetails ??= New<TipDetails>(content);
                ApplyText(values, "situation", value => content.TipDetails.Situation = value);
                ApplyText(values, "keyPoint", value => content.TipDetails.KeyPoint = value);
                ApplyText(values, "precautions", value => content.TipDetails.Precautions = value);
                break;
            case "OTHER":
                content.OtherDetails ??= New<OtherDetails>(content);
                ApplyText(values, "customLabel", value => content.OtherDetails.CustomLabel = value);
                ApplyText(values, "additionalInfo", value => content.OtherDetails.AdditionalInfo = value);
                break;
        }
    }

    private static T New<T>(Content content) where T : ContentDetailsBase, new() =>
        new() { ContentId = content.Id, Content = content };

    private static void ApplyText(IReadOnlyDictionary<string, string?> values, string key, Action<string?> assign)
    {
        if (values.TryGetValue(key, out var value)) assign(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    private static void ApplyInt(IReadOnlyDictionary<string, string?> values, string key, Action<int?> assign)
    {
        if (!values.TryGetValue(key, out var value)) return;
        assign(string.IsNullOrWhiteSpace(value) ? null : int.Parse(value, CultureInfo.InvariantCulture));
    }

    private static void ApplyDecimal(IReadOnlyDictionary<string, string?> values, string key, Action<decimal?> assign)
    {
        if (!values.TryGetValue(key, out var value)) return;
        assign(string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture));
    }

    private static CategoryEditDto Map(Content content) => new(
        content.Id,
        content.Title,
        content.ShortSummary,
        content.CategoryId,
        CategoryCatalog.Get(content.CategoryId).Code,
        content.CurrentWorkflowStep,
        Convert.ToBase64String(content.RowVersion),
        MapAll(content));

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> MapAll(Content content)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal);
        if (content.PlaceDetails is { } place) result["PLACE"] = Values(("address", place.Address), ("businessHours", place.BusinessHours), ("parkingInfo", place.ParkingInfo), ("recommendedMenuOrSpot", place.RecommendedMenuOrSpot));
        if (content.CookingDetails is { } cooking) result["COOKING"] = Values(("servings", Number(cooking.Servings)), ("preparationMinutes", Number(cooking.PreparationMinutes)), ("cookingMinutes", Number(cooking.CookingMinutes)), ("difficulty", cooking.Difficulty));
        if (content.ExerciseDetails is { } exercise) result["EXERCISE"] = Values(("targetArea", exercise.TargetArea), ("durationMinutes", Number(exercise.DurationMinutes)), ("difficulty", exercise.Difficulty), ("equipment", exercise.Equipment));
        if (content.CleaningLaundryDetails is { } cleaning) result["CLEANING_LAUNDRY"] = Values(("target", cleaning.Target), ("supplies", cleaning.Supplies), ("precautions", cleaning.Precautions));
        if (content.TravelDetails is { } travel) result["TRAVEL"] = Values(("destination", travel.Destination), ("bestSeason", travel.BestSeason), ("transportation", travel.Transportation), ("budgetNote", travel.BudgetNote));
        if (content.PhotoDetails is { } photo) result["PHOTO"] = Values(("camera", photo.Camera), ("lens", photo.Lens), ("shootingSettings", photo.ShootingSettings), ("location", photo.Location));
        if (content.StudyDetails is { } study) result["STUDY"] = Values(("subject", study.Subject), ("learningGoal", study.LearningGoal), ("resource", study.Resource), ("reviewCycle", study.ReviewCycle));
        if (content.ProductDetails is { } product) result["PRODUCT"] = Values(("brand", product.Brand), ("modelName", product.ModelName), ("price", product.Price?.ToString(CultureInfo.InvariantCulture)), ("purchasePlace", product.PurchasePlace));
        if (content.PhoneComputerDetails is { } phone) result["PHONE_COMPUTER"] = Values(("deviceOrOs", phone.DeviceOrOs), ("appOrProgram", phone.AppOrProgram), ("problem", phone.Problem), ("solution", phone.Solution));
        if (content.TipDetails is { } tip) result["TIP"] = Values(("situation", tip.Situation), ("keyPoint", tip.KeyPoint), ("precautions", tip.Precautions));
        if (content.OtherDetails is { } other) result["OTHER"] = Values(("customLabel", other.CustomLabel), ("additionalInfo", other.AdditionalInfo));
        return result;
    }

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static IReadOnlyDictionary<string, string?> Values(params (string Key, string? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
    private static IReadOnlySet<string> Fields(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
