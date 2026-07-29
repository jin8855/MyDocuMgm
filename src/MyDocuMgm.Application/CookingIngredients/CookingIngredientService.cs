using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.CookingIngredients;

public sealed class CookingIngredientService(ICookingIngredientRepository repository)
{
    public async Task<IReadOnlyList<CookingIngredientDto>> ListAsync(Guid contentId, CancellationToken cancellationToken)
    {
        await EnsureCookingContentAsync(contentId, cancellationToken);
        return (await repository.ListAsync(contentId, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<CookingIngredientDto> AddAsync(
        Guid contentId,
        SaveCookingIngredientRequest request,
        CancellationToken cancellationToken)
    {
        var content = await EnsureCookingContentAsync(contentId, cancellationToken);
        content.CookingDetails ??= new CookingDetails { ContentId = contentId, Content = content };
        var existing = await repository.ListAsync(contentId, cancellationToken);
        var ingredient = new CookingIngredient
        {
            ContentId = contentId,
            SortOrder = existing.Count + 1
        };
        Apply(ingredient, request);
        await repository.AddAsync(ingredient, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(ingredient);
    }

    public async Task<CookingIngredientDto> UpdateAsync(
        Guid contentId,
        Guid ingredientId,
        SaveCookingIngredientRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCookingContentAsync(contentId, cancellationToken);
        var ingredient = await repository.FindAsync(contentId, ingredientId, cancellationToken)
            ?? throw new NotFoundException("재료를 찾을 수 없습니다.");
        EnsureRowVersion(ingredient, request.RowVersion);
        Apply(ingredient, request);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(ingredient);
    }

    public async Task DeleteAsync(
        Guid contentId,
        Guid ingredientId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        await EnsureCookingContentAsync(contentId, cancellationToken);
        var ingredient = await repository.FindAsync(contentId, ingredientId, cancellationToken)
            ?? throw new NotFoundException("재료를 찾을 수 없습니다.");
        EnsureRowVersion(ingredient, rowVersion);
        repository.Remove(ingredient);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        Guid contentId,
        ReorderCookingIngredientsRequest request,
        CancellationToken cancellationToken)
    {
        var ingredients = await repository.ListAsync(contentId, cancellationToken);
        if (ingredients.Count != request.IngredientIds.Count ||
            request.IngredientIds.Distinct().Count() != request.IngredientIds.Count ||
            ingredients.Any(ingredient => !request.IngredientIds.Contains(ingredient.Id)))
        {
            throw new DomainRuleException("INVALID_INGREDIENT_ORDER", "정렬 목록은 현재 재료 전체를 중복 없이 포함해야 합니다.");
        }

        for (var index = 0; index < request.IngredientIds.Count; index++)
        {
            ingredients.Single(value => value.Id == request.IngredientIds[index]).SortOrder = index + 1;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Content> EnsureCookingContentAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await repository.FindCookingContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        var cookingCategory = CategoryCatalog.All.Single(value => value.Code == "COOKING");
        if (content.CategoryId != cookingCategory.Id)
        {
            throw new DomainRuleException("CONTENT_NOT_COOKING", "요리 분류 콘텐츠에서만 재료를 변경할 수 있습니다.");
        }

        return content;
    }

    private static void Apply(CookingIngredient ingredient, SaveCookingIngredientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            throw new DomainRuleException("INVALID_INGREDIENT_NAME", "재료명은 1~200자여야 합니다.");
        }

        if (request.Quantity?.Length > 100 || request.Note?.Length > 500 ||
            request.IngredientType is not ("주재료" or "부재료" or "확인 필요"))
        {
            throw new DomainRuleException("INVALID_INGREDIENT", "재료 분량, 구분 또는 메모가 올바르지 않습니다.");
        }

        ingredient.Name = request.Name.Trim();
        ingredient.Quantity = request.Quantity?.Trim();
        ingredient.IngredientType = request.IngredientType;
        ingredient.IsPrimary = request.IsPrimary;
        ingredient.Note = request.Note?.Trim();
    }

    private static void EnsureRowVersion(CookingIngredient ingredient, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
        {
            throw new ConcurrencyConflictException("재료 수정에는 rowversion이 필요합니다.");
        }

        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(supplied);
        }
        catch (FormatException)
        {
            throw new ConcurrencyConflictException("재료 rowversion 형식이 올바르지 않습니다.");
        }

        if (!ingredient.RowVersion.AsSpan().SequenceEqual(parsed))
        {
            throw new ConcurrencyConflictException("다른 변경이 먼저 저장되었습니다.");
        }
    }

    private static CookingIngredientDto Map(CookingIngredient value) =>
        new(
            value.Id,
            value.SortOrder,
            value.Name,
            value.Quantity,
            value.IngredientType,
            value.IsPrimary,
            value.Note,
            Convert.ToBase64String(value.RowVersion));
}
