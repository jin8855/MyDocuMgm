using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MyDocuMgm.Application.ExternalFetch;

namespace MyDocuMgm.Infrastructure.ExternalFetch;

public sealed partial class HtmlContentExtractor : IHtmlContentExtractor
{
    private static readonly string[] RemovedSelectors =
    [
        "script", "style", "noscript", "template", "svg", "nav",
        "header", "footer", "aside", "form", "dialog", "iframe"
    ];

    public ExtractedPageContent Extract(string html, Uri finalUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(finalUri);

        var document = new HtmlParser().ParseDocument(html);
        var jsonLd = ReadJsonLd(document);
        var title = First(
            Meta(document, "property", "og:title"),
            JsonLdString(jsonLd, "headline"),
            document.Title);
        var description = First(
            Meta(document, "property", "og:description"),
            Meta(document, "name", "description"),
            JsonLdString(jsonLd, "description"));
        var author = First(
            Meta(document, "name", "author"),
            Meta(document, "property", "article:author"),
            JsonLdAuthor(jsonLd));
        var published = ParseDate(First(
            Meta(document, "property", "article:published_time"),
            JsonLdString(jsonLd, "datePublished")));

        var root = document.QuerySelector("main, article, [role='main']") ?? document.Body;
        if (root is null)
        {
            return new(title, description, author, published, string.Empty);
        }

        foreach (var selector in RemovedSelectors)
        {
            foreach (var element in root.QuerySelectorAll(selector).ToArray())
            {
                element.Remove();
            }
        }

        var blocks = root.QuerySelectorAll("h1, h2, h3, h4, h5, h6, p, li, blockquote")
            .Select(element => Normalize(element.TextContent))
            .Where(value => value.Length > 0)
            .ToArray();
        var body = blocks.Length > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, blocks)
            : Normalize(root.TextContent);

        return new(title, description, author, published, Limit(body, 20_000));
    }

    private static string? Meta(IDocument document, string attribute, string key) =>
        document.QuerySelector($"meta[{attribute}='{key}' i]")?.GetAttribute("content");

    private static IReadOnlyList<JsonElement> ReadJsonLd(IDocument document)
    {
        var values = new List<JsonElement>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json' i]"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                Collect(json.RootElement, values);
            }
            catch (JsonException)
            {
                // Invalid JSON-LD does not make otherwise usable HTML fail.
            }
        }

        return values;
    }

    private static void Collect(JsonElement element, ICollection<JsonElement> values)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Collect(item, values);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        values.Add(element.Clone());
        if (element.TryGetProperty("@graph", out var graph))
        {
            Collect(graph, values);
        }
    }

    private static string? JsonLdString(IEnumerable<JsonElement> values, string propertyName)
    {
        foreach (var value in values)
        {
            if (value.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static string? JsonLdAuthor(IEnumerable<JsonElement> values)
    {
        foreach (var value in values)
        {
            if (!value.TryGetProperty("author", out var author))
            {
                continue;
            }

            if (author.ValueKind == JsonValueKind.String)
            {
                return author.GetString();
            }

            if (author.ValueKind == JsonValueKind.Object &&
                author.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                return name.GetString();
            }
        }

        return null;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    private static string? First(params string?[] values) =>
        values.Select(Normalize).FirstOrDefault(value => value.Length > 0);

    private static string Normalize(string? value) =>
        Whitespace().Replace(value ?? string.Empty, " ").Trim();

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
