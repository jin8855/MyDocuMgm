using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.CookingIngredients;

public sealed record CookingIngredientDto(
    Guid Id,
    int SortOrder,
    string Name,
    string? Quantity,
    string IngredientType,
    bool IsPrimary,
    string? Note,
    string RowVersion);

public sealed record SaveCookingIngredientRequest(
    string Name,
    string? Quantity,
    string IngredientType,
    bool IsPrimary,
    string? Note,
    string? RowVersion);

public sealed record ReorderCookingIngredientsRequest(IReadOnlyList<Guid> IngredientIds);

public interface ICookingIngredientRepository
{
    Task<Content?> FindCookingContentAsync(Guid contentId, CancellationToken cancellationToken);
    Task<CookingIngredient?> FindAsync(Guid contentId, Guid ingredientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CookingIngredient>> ListAsync(Guid contentId, CancellationToken cancellationToken);
    Task AddAsync(CookingIngredient ingredient, CancellationToken cancellationToken);
    void Remove(CookingIngredient ingredient);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
