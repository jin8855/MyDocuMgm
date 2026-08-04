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
}

internal sealed class InMemoryUrlIntakeRepository(params Guid[] mediaIds) : IUrlIntakeRepository
{
    private readonly ConcurrentDictionary<string, Content> _byUrl = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _mediaIds = [.. mediaIds];

    public IReadOnlyCollection<Content> Contents => _byUrl.Values.ToArray();
    public IReadOnlyCollection<Guid> MediaIds => _mediaIds;

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

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
