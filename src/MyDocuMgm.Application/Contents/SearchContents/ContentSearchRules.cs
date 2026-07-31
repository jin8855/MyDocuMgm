using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.Contents.SearchContents;

public static class ContentSearchRules
{
    public static void Validate(ContentQuery query)
    {
        if (query.Page < 1)
        {
            throw new DomainRuleException("INVALID_PAGE", "페이지는 1 이상이어야 합니다.");
        }

        if (query.PageSize is not (24 or 48 or 96))
        {
            throw new DomainRuleException("INVALID_PAGE_SIZE", "페이지 크기는 24, 48, 96만 허용합니다.");
        }

        if (!string.IsNullOrWhiteSpace(query.MajorCategory) &&
            CategoryCatalog.All.All(category => category.Code != query.MajorCategory))
        {
            throw new DomainRuleException("INVALID_MAJOR_CATEGORY", "알 수 없는 대분류 코드입니다.");
        }

        if (query.WorkflowStep is { } workflowStep && !Enum.IsDefined(workflowStep))
        {
            throw new DomainRuleException("INVALID_WORKFLOW_STEP", "알 수 없는 workflow 단계입니다.");
        }

        if (!string.IsNullOrWhiteSpace(query.AttributeKey))
        {
            if (string.IsNullOrWhiteSpace(query.MajorCategory) ||
                !CategorySearchAttributeCatalog.IsAllowed(query.MajorCategory, query.AttributeKey))
            {
                throw new DomainRuleException("INVALID_ATTRIBUTE_KEY", "허용되지 않은 검색 속성입니다.");
            }
        }
    }
}
