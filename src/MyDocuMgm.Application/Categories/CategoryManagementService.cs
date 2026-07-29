using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Categories;

public sealed class CategoryManagementService(ICategoryManagementRepository repository)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<CategoryDto> UpdateAsync(
        string code,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await FindFixedCategoryAsync(code, cancellationToken);
        EnsureRowVersion(category.RowVersion, request.RowVersion);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 80 ||
            request.SortOrder is < 1 or > 11)
        {
            throw new DomainRuleException("INVALID_CATEGORY_UPDATE", "표시명 또는 정렬 순서가 올바르지 않습니다.");
        }

        if (!request.IsActive && await repository.IsCategoryInUseAsync(category.Id, cancellationToken))
        {
            category.IsActive = false;
        }
        else
        {
            category.IsActive = request.IsActive;
        }

        category.DisplayName = request.DisplayName.Trim();
        category.SortOrder = request.SortOrder;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(category);
    }

    public async Task<IReadOnlyList<CategorySearchAttributeDto>> ListAttributesAsync(
        string code,
        CancellationToken cancellationToken)
    {
        _ = await FindFixedCategoryAsync(code, cancellationToken);
        return (await repository.ListAttributesAsync(code, cancellationToken))
            .Select(attribute => Map(code, attribute))
            .ToArray();
    }

    public async Task<CategorySearchAttributeDto> UpdateAttributeAsync(
        string code,
        string attributeKey,
        UpdateSearchAttributeRequest request,
        CancellationToken cancellationToken)
    {
        _ = await FindFixedCategoryAsync(code, cancellationToken);
        if (!CategorySearchAttributeCatalog.IsAllowed(code, attributeKey))
        {
            throw new DomainRuleException("INVALID_ATTRIBUTE_KEY", "코드에 등록되지 않은 검색 속성입니다.");
        }

        var attribute = await repository.FindAttributeAsync(code, attributeKey, cancellationToken)
            ?? throw new NotFoundException("검색 속성을 찾을 수 없습니다.");
        EnsureRowVersion(attribute.RowVersion, request.RowVersion);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 80 ||
            request.SortOrder < 1)
        {
            throw new DomainRuleException("INVALID_SEARCH_ATTRIBUTE", "검색 속성 표시명 또는 순서가 올바르지 않습니다.");
        }

        attribute.DisplayName = request.DisplayName.Trim();
        attribute.SortOrder = request.SortOrder;
        attribute.IsActive = request.IsActive;
        attribute.IsSearchable = request.IsSearchable;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(code, attribute);
    }

    private async Task<Category> FindFixedCategoryAsync(string code, CancellationToken cancellationToken)
    {
        if (CategoryCatalog.All.All(category => category.Code != code))
        {
            throw new DomainRuleException("FIXED_CATEGORY_ONLY", "11개 고정 분류 코드만 관리할 수 있습니다.");
        }

        return await repository.FindByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("분류를 찾을 수 없습니다.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(supplied);
        }
        catch (FormatException)
        {
            throw new ConcurrencyConflictException("rowversion 형식이 올바르지 않습니다.");
        }

        if (!current.AsSpan().SequenceEqual(parsed))
        {
            throw new ConcurrencyConflictException("다른 변경이 먼저 저장되었습니다.");
        }
    }

    private static CategoryDto Map(Category value) =>
        new(value.Id, value.Code, value.DisplayName, value.SortOrder, value.IsActive, Convert.ToBase64String(value.RowVersion));

    private static CategorySearchAttributeDto Map(string code, CategorySearchAttribute value) =>
        new(
            value.Id,
            code,
            value.AttributeKey,
            value.DisplayName,
            value.SortOrder,
            value.IsActive,
            value.IsSearchable,
            Convert.ToBase64String(value.RowVersion));
}
