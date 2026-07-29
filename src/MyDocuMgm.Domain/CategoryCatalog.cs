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
        new(Guid.Parse("10000000-0000-0000-0000-000000000009"), 9, "PHONE_COMPUTER", "폰&컴"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000010"), 10, "TIP", "팁"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000011"), 11, "OTHER", "기타")
    ];

    public static CategoryDefinition Get(Guid id) =>
        All.SingleOrDefault(category => category.Id == id)
        ?? throw new DomainRuleException("CATEGORY_NOT_FOUND", "고정 분류를 찾을 수 없습니다.");
}

public sealed record CategorySearchAttributeDefinition(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string AttributeKey,
    string DisplayName,
    int SortOrder,
    bool IsActive = true,
    bool IsSearchable = true);

public static class CategorySearchAttributeCatalog
{
    private static Guid Id(int categoryOrder, int attributeOrder) =>
        Guid.Parse($"20000000-0000-0000-{categoryOrder:D4}-{attributeOrder:D12}");

    public static readonly IReadOnlyList<CategorySearchAttributeDefinition> All =
    [
        Define(2, 1, "primaryIngredient", "주재료"),
        Define(2, 2, "difficulty", "난이도"),
        Define(2, 3, "time", "소요시간"),
        Define(8, 1, "brand", "브랜드"),
        Define(8, 2, "store", "구매처"),
        Define(8, 3, "price", "가격대"),
        Define(1, 1, "region", "지역"),
        Define(1, 2, "parking", "주차"),
        Define(5, 1, "destination", "국가·지역"),
        Define(5, 2, "transport", "교통"),
        Define(3, 1, "targetArea", "운동 부위"),
        Define(3, 2, "equipment", "준비물"),
        Define(4, 1, "target", "대상"),
        Define(4, 2, "supplies", "세제·제품"),
        Define(6, 1, "camera", "기기"),
        Define(6, 2, "location", "촬영 장소"),
        Define(7, 1, "subject", "분야"),
        Define(7, 2, "resource", "참고 자료"),
        Define(9, 1, "deviceOrOs", "기기·OS"),
        Define(9, 2, "problem", "문제 유형"),
        Define(10, 1, "situation", "적용 상황"),
        Define(10, 2, "keyPoint", "주제"),
        Define(11, 1, "customLabel", "주제")
    ];

    public static bool IsAllowed(string categoryCode, string attributeKey) =>
        All.Any(value =>
            value.CategoryCode == categoryCode &&
            value.AttributeKey.Equals(attributeKey, StringComparison.Ordinal));

    private static CategorySearchAttributeDefinition Define(
        int categoryOrder,
        int attributeOrder,
        string key,
        string displayName)
    {
        var category = CategoryCatalog.All.Single(value => value.SortOrder == categoryOrder);
        return new(Id(categoryOrder, attributeOrder), category.Id, category.Code, key, displayName, attributeOrder);
    }
}
