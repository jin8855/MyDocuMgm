using MyDocuMgm.Domain;
using MyDocuMgm.Application.Contents.SearchContents;

namespace MyDocuMgm.Application;

public sealed class ContentService(IContentRepository repository)
{
    public Task<PagedResult<ContentSummary>> ListAsync(ContentQuery query, CancellationToken cancellationToken)
    {
        ContentSearchRules.Validate(query);
        return repository.ListAsync(query, cancellationToken);
    }

    public async Task<ContentDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        return Map(content);
    }

    public async Task<ContentDetail> CreateAsync(SaveContentRequest request, CancellationToken cancellationToken)
    {
        Validate(request, isUpdate: false);
        var content = new Content
        {
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            ShortSummary = request.ShortSummary?.Trim(),
            DetailContent = request.DetailContent?.Trim(),
            Visibility = request.Visibility,
            IsFavorite = request.IsFavorite,
            ExperienceStatus = request.ExperienceStatus
        };
        content.SetStatus(request.Status);
        await ReplaceTagsAsync(content, request.Tags, cancellationToken);
        await repository.AddAsync(content, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    public async Task<ContentDetail> UpdateAsync(Guid id, SaveContentRequest request, CancellationToken cancellationToken)
    {
        Validate(request, isUpdate: true);
        var content = await repository.FindAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        EnsureRowVersion(content, request.RowVersion!);
        content.ChangeCategory(request.CategoryId);
        content.Title = request.Title.Trim();
        content.ShortSummary = request.ShortSummary?.Trim();
        content.DetailContent = request.DetailContent?.Trim();
        content.Visibility = request.Visibility;
        content.IsFavorite = request.IsFavorite;
        content.ExperienceStatus = request.ExperienceStatus;
        content.SetStatus(request.Status);
        await ReplaceTagsAsync(content, request.Tags, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    public async Task DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        EnsureRowVersion(content, rowVersion);
        content.SoftDelete();
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContentDetail> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("콘텐츠를 찾을 수 없습니다.");
        content.Restore();
        await repository.SaveChangesAsync(cancellationToken);
        return Map(content);
    }

    private async Task ReplaceTagsAsync(Content content, IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        content.ContentTags.Clear();
        foreach (var name in names?.Where(value => !string.IsNullOrWhiteSpace(value)) ?? [])
        {
            var normalized = TagNormalizer.Normalize(name);
            if (content.ContentTags.Any(link => link.Tag.NormalizedName == normalized))
            {
                continue;
            }

            var tag = await repository.FindTagAsync(normalized, cancellationToken)
                ?? new Tag { Name = name.Trim(), NormalizedName = normalized };
            content.ContentTags.Add(new ContentTag { Content = content, ContentId = content.Id, Tag = tag, TagId = tag.Id });
        }
    }

    private static void Validate(SaveContentRequest request, bool isUpdate)
    {
        _ = CategoryCatalog.Get(request.CategoryId);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
        {
            throw new DomainRuleException("INVALID_TITLE", "제목은 1~200자여야 합니다.");
        }

        if (request.ShortSummary?.Length > 500 || request.DetailContent?.Length > 20_000)
        {
            throw new DomainRuleException("CONTENT_TOO_LONG", "요약 또는 상세 내용이 허용 길이를 초과했습니다.");
        }

        if (!ContentStatusRules.IsPhase1Selectable(request.Status))
        {
            throw new DomainRuleException("STATUS_NOT_SELECTABLE", "Phase 1에서 선택할 수 없는 상태입니다.");
        }

        if (isUpdate && string.IsNullOrWhiteSpace(request.RowVersion))
        {
            throw new ConcurrencyConflictException("수정에는 rowversion이 필요합니다.");
        }
    }

    internal static void EnsureRowVersion(Content content, string supplied)
    {
        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(supplied);
        }
        catch (FormatException exception)
        {
            throw new ConcurrencyConflictException($"rowversion 형식이 올바르지 않습니다: {exception.Message}");
        }

        if (!content.RowVersion.AsSpan().SequenceEqual(parsed))
        {
            throw new ConcurrencyConflictException("다른 변경이 먼저 저장되었습니다. 최신 내용을 다시 불러오세요.");
        }
    }

    internal static ContentDetail Map(Content content) =>
        new(
            content.Id,
            content.CategoryId,
            CategoryCatalog.Get(content.CategoryId).Code,
            content.Title,
            content.ShortSummary,
            content.DetailContent,
            content.Status,
            content.Visibility,
            content.IsFavorite,
            content.ExperienceStatus,
            content.CurrentWorkflowStep,
            content.IsDeleted,
            content.CreatedAtUtc,
            content.UpdatedAtUtc,
            Convert.ToBase64String(content.RowVersion),
            content.ContentTags.Select(link => link.Tag.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray());
}
