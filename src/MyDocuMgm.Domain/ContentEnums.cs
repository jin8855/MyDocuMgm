namespace MyDocuMgm.Domain;

public enum ContentStatus
{
    INBOX,
    REVIEW_REQUIRED,
    READY,
    DRAFTED,
    PUBLISHED,
    ARCHIVED
}

public enum ContentVisibility
{
    PRIVATE,
    PUBLIC_ALLOWED
}

public enum ExperienceStatus
{
    NONE,
    WANT_TO_TRY,
    TRIED
}

public enum MediaStorageStatus
{
    PENDING,
    READY,
    FAILED
}

public enum WorkflowStep
{
    URL,
    ANALYSIS_REVIEW,
    CATEGORY_EDIT,
    MEDIA,
    DETAIL,
    BLOG_DRAFT,
    COMPLETED
}

public enum ContentSourceKind
{
    GENERIC,
    INSTAGRAM
}

public enum InstagramContentType
{
    POST,
    REEL
}

public enum PinnedAuthorCommentState
{
    PRESENT,
    NONE
}

public enum SourceAcquisitionMode
{
    MANUAL
}

public enum IntakeStatus
{
    URL_ACCEPTED,
    MANUAL_INPUT_REQUIRED,
    CONTENT_READY
}

public static class WorkflowStepRules
{
    private static readonly WorkflowStep[] OrderedSteps = Enum.GetValues<WorkflowStep>();

    public static IReadOnlyList<WorkflowStep> All => OrderedSteps;

    public static bool CanMove(WorkflowStep current, WorkflowStep next) =>
        Math.Abs(Array.IndexOf(OrderedSteps, current) - Array.IndexOf(OrderedSteps, next)) <= 1;
}

public static class ContentStatusRules
{
    private static readonly HashSet<ContentStatus> Phase1Selectable =
    [
        ContentStatus.INBOX,
        ContentStatus.REVIEW_REQUIRED,
        ContentStatus.READY,
        ContentStatus.ARCHIVED
    ];

    public static bool IsPhase1Selectable(ContentStatus status) => Phase1Selectable.Contains(status);
}
