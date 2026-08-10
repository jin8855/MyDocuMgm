namespace MyDocuMgm.Domain;

public static class DetailsConsistency
{
    // CategoryId identifies the one active category. Other detail rows are
    // retained as dormant drafts so category changes remain reversible.
    public static void Validate(Content content)
    {
        foreach (var details in new ContentDetailsBase?[]
        {
            content.PlaceDetails, content.CookingDetails, content.ExerciseDetails,
            content.CleaningLaundryDetails, content.TravelDetails, content.PhotoDetails,
            content.StudyDetails, content.ProductDetails, content.PhoneComputerDetails,
            content.TipDetails, content.OtherDetails
        }.Where(value => value is not null))
        {
            if (details!.ContentId != Guid.Empty && details.ContentId != content.Id)
            {
                throw new DomainRuleException("CATEGORY_DETAIL_CONTENT_MISMATCH", "분류별 상세정보의 콘텐츠 소유권이 일치하지 않습니다.");
            }
        }
    }
}
