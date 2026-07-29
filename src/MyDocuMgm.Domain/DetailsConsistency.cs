namespace MyDocuMgm.Domain;

public static class DetailsConsistency
{
    public static void Validate(Content content)
    {
        var present = new[]
        {
            (Code: "PLACE", Present: content.PlaceDetails is not null),
            (Code: "COOKING", Present: content.CookingDetails is not null),
            (Code: "EXERCISE", Present: content.ExerciseDetails is not null),
            (Code: "CLEANING_LAUNDRY", Present: content.CleaningLaundryDetails is not null),
            (Code: "TRAVEL", Present: content.TravelDetails is not null),
            (Code: "PHOTO", Present: content.PhotoDetails is not null),
            (Code: "STUDY", Present: content.StudyDetails is not null),
            (Code: "PRODUCT", Present: content.ProductDetails is not null),
            (Code: "PHONE_COMPUTER", Present: content.PhoneComputerDetails is not null),
            (Code: "TIP", Present: content.TipDetails is not null),
            (Code: "OTHER", Present: content.OtherDetails is not null)
        }.Where(value => value.Present).Select(value => value.Code).ToArray();

        if (present.Length > 1 || (present.Length == 1 && present[0] != CategoryCatalog.Get(content.CategoryId).Code))
        {
            throw new DomainRuleException(
                "CATEGORY_DETAIL_MISMATCH",
                "분류와 상세정보가 일치하지 않습니다. 충돌 항목을 명시적으로 수정하거나 제거하세요.");
        }
    }
}
