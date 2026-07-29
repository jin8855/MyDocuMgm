using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Categories;

public sealed record CategoryDto(
    Guid Id,
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsActive,
    string RowVersion);

public sealed record CategorySearchAttributeDto(
    Guid Id,
    string CategoryCode,
    string AttributeKey,
    string DisplayName,
    int SortOrder,
    bool IsActive,
    bool IsSearchable,
    string RowVersion);

public sealed record UpdateCategoryRequest(
    string DisplayName,
    int SortOrder,
    bool IsActive,
    string RowVersion);

public sealed record UpdateSearchAttributeRequest(
    string DisplayName,
    int SortOrder,
    bool IsActive,
    bool IsSearchable,
    string RowVersion);

public interface ICategoryManagementRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
    Task<Category?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategorySearchAttribute>> ListAttributesAsync(string categoryCode, CancellationToken cancellationToken);
    Task<CategorySearchAttribute?> FindAttributeAsync(string categoryCode, string attributeKey, CancellationToken cancellationToken);
    Task<bool> IsCategoryInUseAsync(Guid categoryId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
