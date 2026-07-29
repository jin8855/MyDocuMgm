namespace MyDocuMgm.Domain;

public sealed record CategoryDefinition(Guid Id, int SortOrder, string Code, string DisplayName);

public static class CategoryCatalog
{
    public static readonly IReadOnlyList<CategoryDefinition> All =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), 1, "PLACE", "가볼곳"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), 2, "COOKING", "요리"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), 3, "EXERCISE", "운동"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), 4, "CLEANING_LAUNDRY", "청소&세탁"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000005"), 5, "TRAVEL", "여행"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000006"), 6, "PHOTO", "사진"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000007"), 7, "STUDY", "공부"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000008"), 8, "PRODUCT", "제품"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000009"), 9, "PHONE_COMPUTER", "폰·컴퓨터"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000010"), 10, "TIP", "팁"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000011"), 11, "OTHER", "기타")
    ];

    public static CategoryDefinition Get(Guid id) =>
        All.SingleOrDefault(category => category.Id == id)
        ?? throw new DomainRuleException("CATEGORY_NOT_FOUND", "고정 분류를 찾을 수 없습니다.");
}
