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
