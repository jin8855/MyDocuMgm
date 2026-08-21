using MyDocuMgm.Domain;

namespace MyDocuMgm.Application;

internal static class ContentTagUpdater
{
    public static async Task ReplaceAsync(
        Content content,
        IReadOnlyList<string>? names,
        IContentTagRepository repository,
        CancellationToken cancellationToken)
    {
        var requested = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names?.Where(value => !string.IsNullOrWhiteSpace(value)) ?? [])
        {
            var trimmed = name.Trim();
            var normalized = TagNormalizer.Normalize(name);
            if (trimmed.Length > 80 || normalized.Length > 80)
            {
                throw new DomainRuleException("INVALID_TAG", "태그는 1~80자여야 합니다.");
            }

            requested.TryAdd(normalized, trimmed);
        }

        var existing = content.ContentTags.ToDictionary(
            link => link.Tag.NormalizedName,
            StringComparer.Ordinal);

        foreach (var link in content.ContentTags
                     .Where(link => !requested.ContainsKey(link.Tag.NormalizedName))
                     .ToArray())
        {
            content.ContentTags.Remove(link);
        }

        foreach (var (normalized, displayName) in requested)
        {
            if (existing.ContainsKey(normalized))
            {
                continue;
            }

            var tag = await repository.FindTagAsync(normalized, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = displayName, NormalizedName = normalized };
                await repository.AddTagAsync(tag, cancellationToken);
            }

            content.ContentTags.Add(new ContentTag
            {
                Content = content,
                ContentId = content.Id,
                Tag = tag,
                TagId = tag.Id
            });
        }
    }
}
