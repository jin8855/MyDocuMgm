using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyDocuMgm.Application.Contents.EditImage;
using MyDocuMgm.Domain;

namespace MyDocuMgm.IntegrationTests;

public sealed class ImageStageApiTests : IDisposable
{
    private readonly Content _content = new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "이미지 API 테스트",
        CurrentWorkflowStep = WorkflowStep.MEDIA,
        RowVersion = [7, 8, 9]
    };
    private readonly MediaAsset _ownedMedia;
    private readonly Repository _repository;
    private readonly WebApplicationFactory<Program> _factory;

    public ImageStageApiTests()
    {
        _ownedMedia = new MediaAsset
        {
            ContentId = _content.Id,
            OriginalFileName = "api.png",
            StoredFileName = "api.png",
            RelativePath = $"media/{_content.Id:N}/{Guid.NewGuid():N}/original/api.png",
            MimeType = "image/png",
            SizeBytes = 68,
            Sha256 = new string('B', 64),
            Width = 1,
            Height = 1,
            SortOrder = 1,
            StorageStatus = MediaStorageStatus.READY
        };
        _repository = new Repository(_content, [_ownedMedia]);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IImageStageRepository>();
                services.AddSingleton<IImageStageRepository>(_repository);
            });
        });
    }

    [Fact]
    public async Task DraftThenGet_RestoresTheLinkedImageAndStaysInMedia()
    {
        using var client = _factory.CreateClient();

        using var saved = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/image-stage", new
        {
            mediaIds = new[] { _ownedMedia.Id, _ownedMedia.Id },
            complete = false,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        using var loaded = await client.GetAsync($"/api/contents/{_content.Id}/image-stage");
        var result = await loaded.Content.ReadFromJsonAsync<ImageStageDto>();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal(WorkflowStep.MEDIA, result!.CurrentWorkflowStep);
        Assert.Equal([_ownedMedia.Id], result.LinkedMediaIds);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Complete_AtomicallyAdvancesToDetail()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/image-stage", new
        {
            mediaIds = Array.Empty<Guid>(),
            complete = true,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        var result = await response.Content.ReadFromJsonAsync<ImageStageDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkflowStep.DETAIL, result!.CurrentWorkflowStep);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task ForeignMediaAndEntryBypass_AreRejectedBeforeSave()
    {
        using var client = _factory.CreateClient();
        using var foreign = await client.PutAsJsonAsync($"/api/contents/{_content.Id}/image-stage", new
        {
            mediaIds = new[] { Guid.NewGuid() },
            complete = false,
            rowVersion = Convert.ToBase64String(_content.RowVersion)
        });
        _content.CurrentWorkflowStep = WorkflowStep.CATEGORY_EDIT;
        using var bypass = await client.GetAsync($"/api/contents/{_content.Id}/image-stage");

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bypass.StatusCode);
        Assert.Equal(0, _repository.SaveCount);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class Repository(Content content, IReadOnlyList<MediaAsset> media) : IImageStageRepository
    {
        public int SaveCount { get; private set; }

        public Task<ImageStageData?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<ImageStageData?>(contentId == content.Id ? new(content, media) : null);

        public void RemoveLinks(IReadOnlyCollection<ContentMediaLink> links) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
