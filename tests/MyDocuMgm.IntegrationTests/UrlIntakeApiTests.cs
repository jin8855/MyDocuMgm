using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class UrlIntakeApiTests : IDisposable
{
    private readonly Guid _mediaId = Guid.NewGuid();
    private readonly ConcurrentUrlIntakeRepository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public UrlIntakeApiTests()
    {
        _repository = new ConcurrentUrlIntakeRepository(_mediaId);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUrlIntakeRepository>();
                services.AddSingleton<IUrlIntakeRepository>(_repository);
            });
        });
    }

    [Fact]
    public async Task NewUrl_IsPersistedAndReloadedWithoutExternalRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/url-intakes",
            new { url = " https://Example.com:443/Path?q=1#fragment " });
        var created = await response.Content.ReadFromJsonAsync<UrlIntakeDto>();
        var reloaded = await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{created!.Id}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("https://example.com/Path?q=1", created.NormalizedUrl);
        Assert.Equal("URL_ACCEPTED", created.Status);
        Assert.Equal(created.Id, reloaded!.Id);
        Assert.Equal(created.OriginalUrl, reloaded.OriginalUrl);
        Assert.Equal(created.NormalizedUrl, reloaded.NormalizedUrl);
        Assert.Equal(created.Status, reloaded.Status);
        Assert.Equal(created.LinkedMediaIds, reloaded.LinkedMediaIds);
        Assert.Equal(0, _repository.ExternalRequestCount);
    }

    [Fact]
    public async Task EquivalentAndConcurrentUrls_CreateOneRecordAndReturnExistingId()
    {
        using var client = _factory.CreateClient();
        var requests = Enumerable.Range(0, 20)
            .Select(index => client.PostAsJsonAsync(
                "/api/url-intakes",
                new { url = $"https://WWW.INSTAGRAM.com/reel/AbC_12/?share={index}#x" }))
            .ToArray();

        var responses = await Task.WhenAll(requests);
        var results = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<UrlIntakeDto>()));

        Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
        Assert.Single(results.Select(result => result!.Id).Distinct());
        Assert.Single(_repository.Contents);
        Assert.Single(results, result => result!.IsDuplicate is false);
        Assert.Equal(19, results.Count(result => result!.IsDuplicate));
    }

    [Fact]
    public async Task InvalidUrl_DoesNotCreateRecord()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/url-intakes",
            new { url = "file:///c:/private.txt" });
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code", problem!);
        Assert.Empty(_repository.Contents);
    }

    [Fact]
    public async Task ManualBody_BlankDoesNotAdvanceAndValidBodySurvivesReload()
    {
        using var client = _factory.CreateClient();
        var intake = await CreateAsync(client, "https://example.com/manual");

        var manual = await client.PostAsync($"/api/url-intakes/{intake.Id}/manual-input", null);
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        using var blank = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/manual-body",
            new { body = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(
            "MANUAL_INPUT_REQUIRED",
            (await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}"))!.Status);

        using var savedResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/manual-body",
            new { body = "  synthetic manual content  " });
        var saved = await savedResponse.Content.ReadFromJsonAsync<UrlIntakeDto>();
        var reloaded = await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}");

        Assert.Equal("CONTENT_READY", saved!.Status);
        Assert.Equal("synthetic manual content", reloaded!.ManualBody);
        Assert.True(reloaded.ManualBodyPresent);
    }

    [Fact]
    public async Task ManualBody_AboveExistingLimit_ReturnsValidationProblemAndDoesNotAdvance()
    {
        using var client = _factory.CreateClient();
        var intake = await CreateAsync(client, "https://example.com/manual-limit");
        await client.PostAsync($"/api/url-intakes/{intake.Id}/manual-input", null);

        using var response = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/manual-body",
            new { body = new string('x', Content.ManualBodyMaxLength + 1) });
        var reloaded = await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MANUAL_INPUT_REQUIRED", reloaded!.Status);
        Assert.False(reloaded.ManualBodyPresent);
    }

    [Fact]
    public async Task NullManualBodyAndMediaIds_ReturnValidationProblemsInsteadOfServerErrors()
    {
        using var client = _factory.CreateClient();
        var intake = await CreateAsync(client, "https://example.com/null-payloads");
        await client.PostAsync($"/api/url-intakes/{intake.Id}/manual-input", null);

        using var bodyResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/manual-body",
            new { body = (string?)null });
        using var mediaResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/media-links",
            new { mediaIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.BadRequest, bodyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mediaResponse.StatusCode);
    }

    [Fact]
    public async Task ExistingMedia_LinkDuplicateUnlinkAndReload_PreserveMediaRecord()
    {
        using var client = _factory.CreateClient();
        var intake = await CreateAsync(client, "https://example.com/link-media");

        using var linkResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/media-links",
            new { mediaIds = new[] { _mediaId, _mediaId } });
        var linked = await linkResponse.Content.ReadFromJsonAsync<UrlIntakeDto>();
        var reloaded = await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}");
        Assert.Equal([_mediaId], linked!.LinkedMediaIds);
        Assert.Equal(linked.LinkedMediaIds, reloaded!.LinkedMediaIds);

        using var unlinkResponse = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/media-links",
            new { mediaIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);
        Assert.True(_repository.MediaExists(_mediaId));
        Assert.Empty((await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}"))!.LinkedMediaIds);
    }

    [Fact]
    public async Task MissingMedia_IsRejectedAndLibraryListsOnlyExistingMedia()
    {
        using var client = _factory.CreateClient();
        var intake = await CreateAsync(client, "https://example.com/missing-media");
        var library = await client.GetFromJsonAsync<LinkableMediaPage>(
            "/api/url-intakes/media-library?page=1&pageSize=24");

        using var response = await client.PutAsJsonAsync(
            $"/api/url-intakes/{intake.Id}/media-links",
            new { mediaIds = new[] { Guid.NewGuid() } });

        Assert.Single(library!.Items);
        Assert.Equal(_mediaId, library.Items[0].Id);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<UrlIntakeDto>($"/api/url-intakes/{intake.Id}"))!.LinkedMediaIds);
    }

    private static async Task<UrlIntakeDto> CreateAsync(HttpClient client, string url)
    {
        using var response = await client.PostAsJsonAsync("/api/url-intakes", new { url });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UrlIntakeDto>())!;
    }

    public void Dispose() => _factory.Dispose();
}

internal sealed class ConcurrentUrlIntakeRepository(Guid mediaId) : IUrlIntakeRepository
{
    private readonly ConcurrentDictionary<string, Content> _byUrl = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _mediaIds = [mediaId];
    private readonly object _linkLock = new();

    public IReadOnlyCollection<Content> Contents => _byUrl.Values.ToArray();
    public int ExternalRequestCount => 0;

    public bool MediaExists(Guid id) => _mediaIds.Contains(id);

    public Task<Content?> FindByNormalizedUrlAsync(string normalizedUrl, CancellationToken cancellationToken) =>
        Task.FromResult(_byUrl.TryGetValue(normalizedUrl, out var content) ? content : null);

    public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        Task.FromResult(_byUrl.Values.SingleOrDefault(content => content.Id == contentId));

    public Task<UrlIntakeStoreResult> AddOrGetAsync(Content content, CancellationToken cancellationToken)
    {
        var stored = _byUrl.GetOrAdd(content.NormalizedUrl!, content);
        return Task.FromResult(new UrlIntakeStoreResult(stored, ReferenceEquals(stored, content)));
    }

    public Task<LinkableMediaPage> ListLinkableMediaAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = _mediaIds.Select(id => new LinkableMediaItem(
            id,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "synthetic.png",
            $"/api/contents/10000000-0000-0000-0000-000000000001/media/{id}/thumbnail",
            "image/png",
            128,
            1,
            1)).ToArray();
        return Task.FromResult(new LinkableMediaPage(items, items.Length, page, pageSize));
    }

    public Task ReplaceLinkedMediaAsync(
        Content content,
        IReadOnlyCollection<Guid> requestedMediaIds,
        CancellationToken cancellationToken)
    {
        lock (_linkLock)
        {
            if (requestedMediaIds.Any(id => !_mediaIds.Contains(id)))
            {
                throw new DomainRuleException("MEDIA_LINK_NOT_FOUND", "연결할 수 없는 이미지가 포함되어 있습니다.");
            }

            content.LinkedMedia.Clear();
            foreach (var id in requestedMediaIds.Distinct())
            {
                content.LinkedMedia.Add(new ContentMediaLink { ContentId = content.Id, MediaAssetId = id });
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
