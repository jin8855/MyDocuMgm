using System.Collections.Concurrent;
using MyDocuMgm.Application;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class UrlNormalizerTests
{
    [Theory]
    [InlineData(" http://Example.COM:80/Path?b=2&a=1#part ", "http://example.com/Path?b=2&a=1")]
    [InlineData("https://Example.COM:443", "https://example.com/")]
    [InlineData("https://Example.COM/Path", "https://example.com/Path")]
    [InlineData("https://Example.COM/Path/", "https://example.com/Path/")]
    public void GenericUrl_NormalizesOnlyApprovedComponents(string input, string expected)
    {
        var result = UrlNormalizer.Normalize(input);

        Assert.Equal(input.Trim(), result.OriginalUrl);
        Assert.Equal(expected, result.NormalizedUrl);
        Assert.Equal(ContentSourceKind.GENERIC, result.SourceKind);
    }

    [Theory]
    [InlineData("http://example.com/path")]
    [InlineData("https://example.com/path")]
    public void HttpAndHttps_AreAccepted(string input) =>
        Assert.Equal(input, UrlNormalizer.Normalize(input).NormalizedUrl);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative")]
    [InlineData("file:///c:/secret.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.com/path")]
    [InlineData("https://www.instagram.com:8443/p/AbC/")]
    public void InvalidOrUnsafeUrl_IsRejected(string input) =>
        Assert.Throws<DomainRuleException>(() => UrlNormalizer.Normalize(input));

    [Theory]
    [InlineData("https://instagram.com/p/AbC_12/?utm_source=share#x", "https://www.instagram.com/p/AbC_12/")]
    [InlineData("http://m.instagram.com/reel/Case-Sensitive", "https://www.instagram.com/reel/Case-Sensitive/")]
    [InlineData("https://www.instagram.com/tv/A1_b-2/", "https://www.instagram.com/tv/A1_b-2/")]
    public void InstagramContentUrl_IsCanonical(string input, string expected)
    {
        var result = UrlNormalizer.Normalize(input);

        Assert.Equal(expected, result.NormalizedUrl);
        Assert.Equal(ContentSourceKind.INSTAGRAM, result.SourceKind);
    }

    [Theory]
    [InlineData("https://www.instagram.com/example-profile/")]
    [InlineData("https://www.instagram.com/stories/example/1/")]
    [InlineData("https://www.instagram.com/explore/")]
    [InlineData("https://www.instagram.com/accounts/login/")]
    public void UnsupportedInstagramPath_IsNotStoredAsGeneric(string input)
    {
        var exception = Assert.Throws<DomainRuleException>(() => UrlNormalizer.Normalize(input));

        Assert.Equal("INSTAGRAM_URL_NOT_SUPPORTED", exception.Code);
    }
    [Theory]
    [InlineData("https://instagram.com/p/Post_1/?utm_source=share#caption", "https://www.instagram.com/p/Post_1/", InstagramContentType.POST)]
    [InlineData("https://www.instagram.com/reel/Reel-2/#fragment", "https://www.instagram.com/reel/Reel-2/", InstagramContentType.REEL)]
    public void ManualInstagramPermalink_AcceptsOnlyPostAndReel(
        string input,
        string expected,
        InstagramContentType expectedType)
    {
        var result = UrlNormalizer.NormalizeInstagramPermalink(input);

        Assert.Equal(expected, result.NormalizedUrl);
        Assert.Equal(expectedType, result.InstagramContentType);
    }

    [Theory]
    [InlineData("https://example.com/p/AbC/")]
    [InlineData("https://www.instagram.com/example-profile/")]
    [InlineData("https://www.instagram.com/stories/example/1/")]
    [InlineData("https://www.instagram.com/tv/AbC/")]
    [InlineData("https://m.instagram.com/p/AbC/")]
    [InlineData("https://www.instagram.com/p/")]
    public void ManualInstagramPermalink_RejectsUnsupportedScope(string input) =>
        Assert.Throws<DomainRuleException>(() => UrlNormalizer.NormalizeInstagramPermalink(input));
}

public sealed class UrlIntakeServiceTests
{
    [Fact]
    public async Task NormalizedDuplicate_ReturnsExistingContent()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);

        var first = await service.IntakeAsync(new("https://EXAMPLE.com:443/Path#first"), default);
        var duplicate = await service.IntakeAsync(new("https://example.com/Path#second"), default);

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.Id, duplicate.Id);
        Assert.Single(repository.Contents);
    }

    [Fact]
    public async Task ManualBody_RejectsBlankAndPersistsTrimmedReadyState()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeAsync(new("https://example.com/body"), default);

        var manual = await service.BeginManualInputAsync(intake.Id, default);
        Assert.Equal("MANUAL_INPUT_REQUIRED", manual.Status);
        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualBodyAsync(intake.Id, new("   "), default));

        var saved = await service.SaveManualBodyAsync(intake.Id, new("  synthetic body  "), default);
        var reloaded = await service.GetAsync(intake.Id, default);

        Assert.Equal("CONTENT_READY", saved.Status);
        Assert.Equal("synthetic body", reloaded.ManualBody);
        Assert.True(reloaded.ManualBodyPresent);
    }

    [Fact]
    public async Task ManualBody_RejectsExistingDetailContentLimitWithoutAdvancingState()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeAsync(new("https://example.com/body-limit"), default);
        await service.BeginManualInputAsync(intake.Id, default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualBodyAsync(
                intake.Id,
                new(new string('x', Content.ManualBodyMaxLength + 1)),
                default));
        var reloaded = await service.GetAsync(intake.Id, default);

        Assert.Equal("MANUAL_BODY_TOO_LONG", exception.Code);
        Assert.Equal("MANUAL_INPUT_REQUIRED", reloaded.Status);
        Assert.False(reloaded.ManualBodyPresent);
    }

    [Fact]
    public async Task MediaLinks_AreUniqueAndUnlinkDoesNotDeleteMedia()
    {
        var mediaId = Guid.NewGuid();
        var repository = new InMemoryUrlIntakeRepository(mediaId);
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeAsync(new("https://example.com/media"), default);

        var linked = await service.ReplaceLinkedMediaAsync(
            intake.Id,
            new([mediaId, mediaId]),
            default);
        var unlinked = await service.ReplaceLinkedMediaAsync(intake.Id, new([]), default);

        Assert.Equal([mediaId], linked.LinkedMediaIds);
        Assert.Empty(unlinked.LinkedMediaIds);
        Assert.Contains(mediaId, repository.MediaIds);
    }

    [Fact]
    public async Task UnknownMedia_IsRejectedWithoutChangingLinks()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeAsync(new("https://example.com/missing-media"), default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.ReplaceLinkedMediaAsync(intake.Id, new([Guid.NewGuid()]), default));

        Assert.Equal("MEDIA_LINK_NOT_FOUND", exception.Code);
        Assert.Empty((await service.GetAsync(intake.Id, default)).LinkedMediaIds);
    }
    [Fact]
    public async Task ManualInstagram_PostCaptionPinnedCommentAndMedia_SurviveReload()
    {
        var mediaId = Guid.NewGuid();
        var repository = new InMemoryUrlIntakeRepository(mediaId);
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://instagram.com/p/Post_1/?share=1#fragment"),
            default);

        var saved = await service.SaveManualInstagramAsync(
            intake.Id,
            new("  manual caption  ", "PRESENT", "  author pinned comment  ", [mediaId]),
            default);
        var reloaded = await service.GetAsync(intake.Id, default);

        Assert.Equal("POST", saved.InstagramContentType);
        Assert.Equal("manual caption", reloaded.ManualCaption);
        Assert.Equal("PRESENT", reloaded.PinnedAuthorCommentState);
        Assert.Equal("author pinned comment", reloaded.PinnedAuthorCommentText);
        Assert.Equal("MANUAL", reloaded.SourceAcquisitionMode);
        Assert.Equal([mediaId], reloaded.LinkedMediaIds);
        Assert.Equal("CONTENT_READY", reloaded.Status);
    }

    [Fact]
    public async Task ManualInstagram_PresentRequiresTextAndNoneClearsStaleComment()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://www.instagram.com/reel/Reel_1/"),
            default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualInstagramAsync(
                intake.Id,
                new("caption", "PRESENT", "   ", []),
                default));
        Assert.Equal("PINNED_AUTHOR_COMMENT_REQUIRED", exception.Code);

        await service.SaveManualInstagramAsync(
            intake.Id,
            new("caption", "PRESENT", "old author comment", []),
            default);
        var saved = await service.SaveManualInstagramAsync(
            intake.Id,
            new("updated caption", "NONE", "must be discarded", []),
            default);

        Assert.Equal("REEL", saved.InstagramContentType);
        Assert.Equal("NONE", saved.PinnedAuthorCommentState);
        Assert.Null(saved.PinnedAuthorCommentText);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task ManualInstagram_NumericCommentState_IsRejected(string state)
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://instagram.com/p/NumericState/"),
            default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualInstagramAsync(
                intake.Id,
                new("caption", state, null, []),
                default));

        Assert.Equal("PINNED_AUTHOR_COMMENT_STATE_INVALID", exception.Code);
    }

    [Fact]
    public async Task ManualInstagram_UnknownMedia_DoesNotPartiallyMutateContent()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://instagram.com/p/AtomicMedia/"),
            default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualInstagramAsync(
                intake.Id,
                new("must not persist", "NONE", null, [Guid.NewGuid()]),
                default));
        var reloaded = await service.GetAsync(intake.Id, default);

        Assert.Equal("MEDIA_LINK_NOT_FOUND", exception.Code);
        Assert.Null(reloaded.ManualCaption);
        Assert.Null(reloaded.PinnedAuthorCommentState);
        Assert.Equal("URL_ACCEPTED", reloaded.Status);
        Assert.Empty(reloaded.LinkedMediaIds);
    }
    [Fact]
    public async Task ManualInstagram_CaptionLengthBoundary_UsesUtf16CodeUnits()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://instagram.com/p/CaptionBoundary/"),
            default);
        var koreanLimitMinusOne = new string('가', Content.ManualCaptionMaxLength - 1);
        var emojiAtLimit = string.Concat(
            Enumerable.Repeat("😀", Content.ManualCaptionMaxLength / 2));

        var below = await service.SaveManualInstagramAsync(
            intake.Id,
            new(koreanLimitMinusOne, "NONE", null, []),
            default);
        var exact = await service.SaveManualInstagramAsync(
            intake.Id,
            new(emojiAtLimit, "NONE", null, []),
            default);
        var tooLong = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualInstagramAsync(
                intake.Id,
                new(emojiAtLimit + "가", "NONE", null, []),
                default));
        var preserved = await service.GetAsync(intake.Id, default);

        Assert.Equal(Content.ManualCaptionMaxLength - 1, below.ManualCaption!.Length);
        Assert.Equal(Content.ManualCaptionMaxLength, exact.ManualCaption!.Length);
        Assert.Equal("MANUAL_CAPTION_TOO_LONG", tooLong.Code);
        Assert.Equal(emojiAtLimit, preserved.ManualCaption);
    }

    [Fact]
    public async Task ManualInstagram_PinnedCommentLengthBoundary_UsesUtf16CodeUnits()
    {
        var repository = new InMemoryUrlIntakeRepository();
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://instagram.com/reel/CommentBoundary/"),
            default);
        var koreanLimitMinusOne = new string('한', Content.PinnedAuthorCommentMaxLength - 1);
        var emojiAtLimit = string.Concat(
            Enumerable.Repeat("😀", Content.PinnedAuthorCommentMaxLength / 2));

        var below = await service.SaveManualInstagramAsync(
            intake.Id,
            new("caption", "PRESENT", koreanLimitMinusOne, []),
            default);
        var exact = await service.SaveManualInstagramAsync(
            intake.Id,
            new("caption", "PRESENT", emojiAtLimit, []),
            default);
        var tooLong = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.SaveManualInstagramAsync(
                intake.Id,
                new("caption", "PRESENT", emojiAtLimit + "한", []),
                default));
        var preserved = await service.GetAsync(intake.Id, default);

        Assert.Equal(Content.PinnedAuthorCommentMaxLength - 1, below.PinnedAuthorCommentText!.Length);
        Assert.Equal(Content.PinnedAuthorCommentMaxLength, exact.PinnedAuthorCommentText!.Length);
        Assert.Equal("PINNED_AUTHOR_COMMENT_TOO_LONG", tooLong.Code);
        Assert.Equal(emojiAtLimit, preserved.PinnedAuthorCommentText);
    }
    [Theory]
    [InlineData("begin-manual-input", WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData("save-manual-body", WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData("save-manual-instagram", WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData("replace-linked-media", WorkflowStep.ANALYSIS_REVIEW)]
    [InlineData("begin-manual-input", WorkflowStep.COMPLETED)]
    [InlineData("save-manual-body", WorkflowStep.COMPLETED)]
    [InlineData("save-manual-instagram", WorkflowStep.COMPLETED)]
    [InlineData("replace-linked-media", WorkflowStep.COMPLETED)]
    public async Task UrlStageMutations_RejectLaterStagesBeforeAnyMutation(
        string operation,
        WorkflowStep workflowStep)
    {
        var mediaId = Guid.NewGuid();
        var repository = new InMemoryUrlIntakeRepository(mediaId);
        var service = new UrlIntakeService(repository);
        var intake = await service.IntakeInstagramAsync(
            new("https://www.instagram.com/p/StateGate/"),
            default);
        var content = Assert.Single(repository.Contents);
        content.DetailContent = "preserved body";
        content.ManualCaption = "preserved caption";
        content.PinnedAuthorCommentState = PinnedAuthorCommentState.PRESENT;
        content.PinnedAuthorCommentText = "preserved author comment";
        content.SourceAcquisitionMode = SourceAcquisitionMode.MANUAL;
        content.IntakeStatus = IntakeStatus.CONTENT_READY;
        content.LinkedMedia.Add(new ContentMediaLink
        {
            ContentId = content.Id,
            MediaAssetId = mediaId
        });
        content.CurrentWorkflowStep = workflowStep;
        content.UpdatedAtUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        content.RowVersion = [4, 5, 6];
        var before = UrlMutationSnapshot.Capture(content);

        Task<UrlIntakeDto> MutateAsync() => operation switch
        {
            "begin-manual-input" => service.BeginManualInputAsync(content.Id, default),
            "save-manual-body" => service.SaveManualBodyAsync(
                content.Id,
                new("changed body"),
                default),
            "save-manual-instagram" => service.SaveManualInstagramAsync(
                content.Id,
                new("changed caption", "NONE", null, []),
                default),
            "replace-linked-media" => service.ReplaceLinkedMediaAsync(
                content.Id,
                new([]),
                default),
            _ => throw new InvalidOperationException($"Unknown operation: {operation}")
        };

        var exception = await Assert.ThrowsAsync<DomainRuleException>(MutateAsync);

        Assert.Equal("URL_STAGE_ALREADY_COMPLETED", exception.Code);
        Assert.Equal(before, UrlMutationSnapshot.Capture(content));
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Equal(0, repository.ReplaceLinkedMediaCallCount);
        Assert.Equal(0, repository.SaveManualInstagramCallCount);
    }

    private sealed record UrlMutationSnapshot(
        WorkflowStep WorkflowStep,
        string Title,
        string? OriginalUrl,
        string? NormalizedUrl,
        ContentSourceKind? SourceKind,
        InstagramContentType? InstagramContentType,
        IntakeStatus? IntakeStatus,
        string? DetailContent,
        string? ManualCaption,
        PinnedAuthorCommentState? CommentState,
        string? CommentText,
        SourceAcquisitionMode? AcquisitionMode,
        DateTime UpdatedAtUtc,
        string RowVersion,
        string LinkedMediaIds)
    {
        public static UrlMutationSnapshot Capture(Content content) => new(
            content.CurrentWorkflowStep,
            content.Title,
            content.OriginalUrl,
            content.NormalizedUrl,
            content.SourceKind,
            content.InstagramContentType,
            content.IntakeStatus,
            content.DetailContent,
            content.ManualCaption,
            content.PinnedAuthorCommentState,
            content.PinnedAuthorCommentText,
            content.SourceAcquisitionMode,
            content.UpdatedAtUtc,
            Convert.ToBase64String(content.RowVersion),
            string.Join(",", content.LinkedMedia.Select(link => link.MediaAssetId).Order()));
    }
}

internal sealed class InMemoryUrlIntakeRepository(params Guid[] mediaIds) : IUrlIntakeRepository
{
    private readonly ConcurrentDictionary<string, Content> _byUrl = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _mediaIds = [.. mediaIds];

    public IReadOnlyCollection<Content> Contents => _byUrl.Values.ToArray();
    public IReadOnlyCollection<Guid> MediaIds => _mediaIds;
    public int SaveChangesCallCount { get; private set; }
    public int ReplaceLinkedMediaCallCount { get; private set; }
    public int SaveManualInstagramCallCount { get; private set; }

    public Task<Content?> FindByNormalizedUrlAsync(string normalizedUrl, CancellationToken cancellationToken) =>
        Task.FromResult(_byUrl.TryGetValue(normalizedUrl, out var content) ? content : null);

    public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        Task.FromResult(_byUrl.Values.SingleOrDefault(content => content.Id == contentId));

    public Task<UrlIntakeStoreResult> AddOrGetAsync(Content content, CancellationToken cancellationToken)
    {
        var stored = _byUrl.GetOrAdd(content.NormalizedUrl!, content);
        return Task.FromResult(new UrlIntakeStoreResult(stored, ReferenceEquals(stored, content)));
    }

    public Task<LinkableMediaPage> ListLinkableMediaAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new LinkableMediaPage([], _mediaIds.Count, page, pageSize));

    public Task ReplaceLinkedMediaAsync(
        Content content,
        IReadOnlyCollection<Guid> requestedMediaIds,
        CancellationToken cancellationToken)
    {
        ReplaceLinkedMediaCallCount++;
        if (requestedMediaIds.Any(mediaId => !_mediaIds.Contains(mediaId)))
        {
            throw new DomainRuleException("MEDIA_LINK_NOT_FOUND", "연결할 수 없는 이미지가 포함되어 있습니다.");
        }

        content.LinkedMedia.Clear();
        foreach (var mediaId in requestedMediaIds.Distinct())
        {
            content.LinkedMedia.Add(new ContentMediaLink { ContentId = content.Id, MediaAssetId = mediaId });
        }

        return Task.CompletedTask;
    }

    public Task SaveManualInstagramAsync(
        Content content,
        string caption,
        PinnedAuthorCommentState commentState,
        string? commentText,
        IReadOnlyCollection<Guid> requestedMediaIds,
        CancellationToken cancellationToken)
    {
        SaveManualInstagramCallCount++;
        if (requestedMediaIds.Any(mediaId => !_mediaIds.Contains(mediaId)))
        {
            throw new DomainRuleException("MEDIA_LINK_NOT_FOUND", "A linked media item does not exist.");
        }

        content.SaveManualInstagram(caption, commentState, commentText);
        return ReplaceLinkedMediaAsync(content, requestedMediaIds, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
