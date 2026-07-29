using MyDocuMgm.Application;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class DomainRulesTests
{
    [Fact]
    public void CategoryCatalog_HasElevenStableEntries()
    {
        Assert.Equal(11, CategoryCatalog.All.Count);
        Assert.Equal(Enumerable.Range(1, 11), CategoryCatalog.All.Select(category => category.SortOrder));
        Assert.Equal(11, CategoryCatalog.All.Select(category => category.Id).Distinct().Count());
        Assert.Equal(
            ["PLACE", "COOKING", "EXERCISE", "CLEANING_LAUNDRY", "TRAVEL", "PHOTO", "STUDY", "PRODUCT", "PHONE_COMPUTER", "TIP", "OTHER"],
            CategoryCatalog.All.Select(category => category.Code));
        Assert.Equal(
            ["가볼곳", "요리", "운동", "청소&세탁", "여행", "사진", "공부", "제품", "폰&컴", "팁", "기타"],
            CategoryCatalog.All.Select(category => category.DisplayName));
    }

    [Theory]
    [InlineData(ContentStatus.INBOX, true)]
    [InlineData(ContentStatus.REVIEW_REQUIRED, true)]
    [InlineData(ContentStatus.READY, true)]
    [InlineData(ContentStatus.ARCHIVED, true)]
    [InlineData(ContentStatus.DRAFTED, false)]
    [InlineData(ContentStatus.PUBLISHED, false)]
    public void Phase1StatusSelection_IsExplicit(ContentStatus status, bool expected) =>
        Assert.Equal(expected, ContentStatusRules.IsPhase1Selectable(status));

    [Fact]
    public void CategoryChange_WithDetails_IsBlockedWithoutDeletingDetails()
    {
        var content = new Content
        {
            CategoryId = CategoryCatalog.All[0].Id,
            PlaceDetails = new PlaceDetails()
        };

        var exception = Assert.Throws<DomainRuleException>(() => content.ChangeCategory(CategoryCatalog.All[1].Id));

        Assert.Equal("CATEGORY_DETAIL_CONFLICT", exception.Code);
        Assert.NotNull(content.PlaceDetails);
    }

    [Fact]
    public void DetailsConsistency_BlocksMismatchedCategory()
    {
        var content = new Content
        {
            CategoryId = CategoryCatalog.All.Single(category => category.Code == "COOKING").Id,
            PlaceDetails = new PlaceDetails()
        };

        var exception = Assert.Throws<DomainRuleException>(() => DetailsConsistency.Validate(content));

        Assert.Equal("CATEGORY_DETAIL_MISMATCH", exception.Code);
    }

    [Fact]
    public void TagNormalization_CollapsesWhitespaceCaseAndUnicode()
    {
        var fullWidth = TagNormalizer.Normalize("  Ｌｉｆｅ　 Book  ");
        var ordinary = TagNormalizer.Normalize("life book");

        Assert.Equal("LIFE BOOK", fullWidth);
        Assert.Equal(ordinary, fullWidth);
    }

    [Fact]
    public void Defaults_ArePrivateAndMediaIsNotPublic()
    {
        Assert.Equal(ContentVisibility.PRIVATE, new Content().Visibility);
        Assert.False(new MediaAsset().IsPublicAllowed);
    }

    [Fact]
    public void RowVersionMismatch_HasDedicatedConflict()
    {
        var content = new Content { RowVersion = [1, 2, 3] };

        Assert.Throws<ConcurrencyConflictException>(
            () => ContentService.EnsureRowVersion(content, Convert.ToBase64String([4, 5, 6])));
    }

    [Fact]
    public void SoftDeleteAndRestore_PreserveEntity()
    {
        var content = new Content();
        content.SoftDelete();
        Assert.True(content.IsDeleted);
        Assert.NotNull(content.DeletedAtUtc);

        content.Restore();
        Assert.False(content.IsDeleted);
        Assert.Null(content.DeletedAtUtc);
    }

    [Fact]
    public void Workflow_HasSevenOrderedSteps_AndBlocksSkipping()
    {
        Assert.Equal(
            [WorkflowStep.URL, WorkflowStep.ANALYSIS_REVIEW, WorkflowStep.CATEGORY_EDIT,
             WorkflowStep.MEDIA, WorkflowStep.DETAIL, WorkflowStep.BLOG_DRAFT, WorkflowStep.COMPLETED],
            WorkflowStepRules.All);

        var content = new Content();
        content.MoveTo(WorkflowStep.ANALYSIS_REVIEW);
        Assert.Equal(WorkflowStep.ANALYSIS_REVIEW, content.CurrentWorkflowStep);
        Assert.Throws<DomainRuleException>(() => content.MoveTo(WorkflowStep.DETAIL));
    }

    [Fact]
    public void ContentStep_RejectsDeletedOrForeignMedia()
    {
        var contentId = Guid.NewGuid();
        var step = new ContentStep { ContentId = contentId };
        var deleted = new MediaAsset { ContentId = contentId };
        deleted.SoftDelete();

        Assert.Equal(
            "DELETED_MEDIA_NOT_ASSIGNABLE",
            Assert.Throws<DomainRuleException>(() => step.AssignMedia(deleted)).Code);

        var foreign = new MediaAsset { ContentId = Guid.NewGuid() };
        Assert.Equal(
            "MEDIA_CONTENT_MISMATCH",
            Assert.Throws<DomainRuleException>(() => step.AssignMedia(foreign)).Code);
    }
}
