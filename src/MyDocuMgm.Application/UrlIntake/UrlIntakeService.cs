using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.UrlIntake;

public sealed class UrlIntakeService(IUrlIntakeRepository repository)
{
    public async Task<UrlIntakeDto> IntakeAsync(
        CreateUrlIntakeRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = UrlNormalizer.Normalize(request.Url);
        var existing = await repository.FindByNormalizedUrlAsync(normalized.NormalizedUrl, cancellationToken);
        if (existing is not null)
        {
            return Map(existing, isDuplicate: true);
        }

        var content = new Content
        {
            CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
            Title = BuildProvisionalTitle(normalized.NormalizedUrl)
        };
        content.AcceptUrl(
            normalized.OriginalUrl,
            normalized.NormalizedUrl,
            UrlNormalizer.ComputeHash(normalized.NormalizedUrl),
            normalized.SourceKind);

        var stored = await repository.AddOrGetAsync(content, cancellationToken);
        return Map(stored.Content, isDuplicate: !stored.Created);
    }

    public async Task<UrlIntakeDto> GetAsync(Guid contentId, CancellationToken cancellationToken) =>
        Map(await FindAsync(contentId, cancellationToken), isDuplicate: false);

    public async Task<UrlIntakeDto> BeginManualInputAsync(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        content.RequireManualInput();
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content, isDuplicate: false);
    }

    public async Task<UrlIntakeDto> SaveManualBodyAsync(
        Guid contentId,
        SaveManualBodyRequest request,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        content.SaveManualBody(request.Body);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content, isDuplicate: false);
    }

    public Task<LinkableMediaPage> ListLinkableMediaAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 96)
        {
            throw new DomainRuleException("MEDIA_PAGE_INVALID", "이미지 페이지 범위가 올바르지 않습니다.");
        }

        return repository.ListLinkableMediaAsync(page, pageSize, cancellationToken);
    }

    public async Task<UrlIntakeDto> ReplaceLinkedMediaAsync(
        Guid contentId,
        ReplaceLinkedMediaRequest request,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(contentId, cancellationToken);
        var mediaIds = request.MediaIds?.Distinct().ToArray()
            ?? throw new DomainRuleException(
                "MEDIA_IDS_REQUIRED",
                "연결할 이미지 ID 목록이 필요합니다.");
        await repository.ReplaceLinkedMediaAsync(content, mediaIds, cancellationToken);
        return Map(content, isDuplicate: false);
    }

    private async Task<Content> FindAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        if (content.IntakeStatus is null || content.OriginalUrl is null || content.NormalizedUrl is null || content.SourceKind is null)
        {
            throw new DomainRuleException("URL_INTAKE_NOT_FOUND", "이 콘텐츠에는 URL 접수 정보가 없습니다.");
        }

        return content;
    }

    private static UrlIntakeDto Map(Content content, bool isDuplicate) => new(
        content.Id,
        content.OriginalUrl!,
        content.NormalizedUrl!,
        content.SourceKind!.Value.ToString(),
        content.IntakeStatus!.Value.ToString(),
        isDuplicate,
        content.DetailContent,
        !string.IsNullOrWhiteSpace(content.DetailContent),
        content.LinkedMedia.Select(link => link.MediaAssetId).Order().ToArray());

    private static string BuildProvisionalTitle(string normalizedUrl)
    {
        var host = new Uri(normalizedUrl).Host;
        var value = $"URL 접수 · {host}";
        return value.Length <= 200 ? value : value[..200];
    }
}
