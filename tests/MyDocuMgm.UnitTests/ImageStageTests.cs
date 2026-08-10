using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.EditImage;
using MyDocuMgm.Domain;

namespace MyDocuMgm.UnitTests;

public sealed class ImageStageTests
{
    [Fact]
    public async Task Get_BlocksDirectEntryBeforeCategoryEditCompletes()
    {
        var content = CreateContent(WorkflowStep.CATEGORY_EDIT);
        var service = new ImageStageService(new Repository(content, []));

        var error = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetAsync(content.Id, default));

        Assert.Equal("IMAGE_STAGE_NOT_AVAILABLE", error.Code);
    }

    [Fact]
    public async Task Get_RestoresAnExistingS1LinkEvenWhenTheMediaHasAnotherOwner()
    {
        var content = CreateContent();
        var foreignMedia = CreateMedia(Guid.NewGuid());
        content.LinkedMedia.Add(new ContentMediaLink
        {
            ContentId = content.Id,
            MediaAssetId = foreignMedia.Id
        });
        var service = new ImageStageService(new Repository(content, [foreignMedia]));

        var result = await service.GetAsync(content.Id, default);

        Assert.Equal([foreignMedia.Id], result.LinkedMediaIds);
        Assert.Equal(foreignMedia.ContentId, Assert.Single(result.LinkedMedia).OwnerContentId);
    }

    [Fact]
    public async Task Draft_DeduplicatesIdsAndKeepsMediaStep()
    {
        var content = CreateContent();
        var ownedMedia = CreateMedia(content.Id);
        var repository = new Repository(content, [ownedMedia]);
        var service = new ImageStageService(repository);

        var result = await service.ExecuteAsync(
            content.Id,
            new([ownedMedia.Id, ownedMedia.Id], false, RowVersion(content)),
            default);

        Assert.Equal(WorkflowStep.MEDIA, result.CurrentWorkflowStep);
        Assert.Equal([ownedMedia.Id], result.LinkedMediaIds);
        Assert.Single(content.LinkedMedia);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Draft_RejectsANewForeignContentMediaIdWithoutMutation()
    {
        var content = CreateContent();
        var foreignMedia = CreateMedia(Guid.NewGuid());
        var repository = new Repository(content, []);
        var service = new ImageStageService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new([foreignMedia.Id], false, RowVersion(content)),
            default));

        Assert.Equal("IMAGE_MEDIA_NOT_AVAILABLE", error.Code);
        Assert.Empty(content.LinkedMedia);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_AllowsZeroImagesAndMovesToDetailInOneSave()
    {
        var content = CreateContent();
        var repository = new Repository(content, []);
        var service = new ImageStageService(repository);

        var result = await service.ExecuteAsync(
            content.Id,
            new([], true, RowVersion(content)),
            default);

        Assert.Equal(WorkflowStep.DETAIL, result.CurrentWorkflowStep);
        Assert.Empty(result.LinkedMediaIds);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Unlink_RemovesOnlyTheRelationAndDoesNotMutateTheMediaAsset()
    {
        var content = CreateContent();
        var media = CreateMedia(content.Id);
        content.LinkedMedia.Add(new ContentMediaLink { ContentId = content.Id, MediaAssetId = media.Id });
        var repository = new Repository(content, [media]);
        var service = new ImageStageService(repository);

        await service.ExecuteAsync(content.Id, new([], false, RowVersion(content)), default);

        Assert.Empty(content.LinkedMedia);
        Assert.Equal(MediaStorageStatus.READY, media.StorageStatus);
        Assert.False(media.IsDeleted);
        Assert.Equal(1, repository.RemovedLinkCount);
    }

    [Fact]
    public async Task SaveFailure_PreservesDurableLinksAndWorkflow()
    {
        var durable = CreateContent();
        var media = CreateMedia(durable.Id);
        durable.LinkedMedia.Add(new ContentMediaLink { ContentId = durable.Id, MediaAssetId = media.Id });
        var repository = new Repository(durable, [media], failSave: true, returnClone: true);
        var service = new ImageStageService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            durable.Id,
            new([], true, RowVersion(durable)),
            default));

        Assert.Equal(WorkflowStep.MEDIA, durable.CurrentWorkflowStep);
        Assert.Equal(media.Id, Assert.Single(durable.LinkedMedia).MediaAssetId);
    }

    [Fact]
    public async Task SaveAfterCompletion_IsRejected()
    {
        var content = CreateContent(WorkflowStep.DETAIL);
        var repository = new Repository(content, []);
        var service = new ImageStageService(repository);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ExecuteAsync(
            content.Id,
            new([], false, RowVersion(content)),
            default));

        Assert.Equal("IMAGE_STAGE_ALREADY_COMPLETED", error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Content CreateContent(WorkflowStep step = WorkflowStep.MEDIA) => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "이미지 단계 테스트",
        CurrentWorkflowStep = step,
        RowVersion = [1, 2, 3]
    };

    private static MediaAsset CreateMedia(Guid ownerContentId) => new()
    {
        ContentId = ownerContentId,
        OriginalFileName = "synthetic.png",
        StoredFileName = $"{Guid.NewGuid():N}.png",
        RelativePath = $"media/{ownerContentId:N}/{Guid.NewGuid():N}/original/synthetic.png",
        MimeType = "image/png",
        SizeBytes = 68,
        Sha256 = new string('A', 64),
        Width = 1,
        Height = 1,
        SortOrder = 1,
        StorageStatus = MediaStorageStatus.READY
    };

    private static string RowVersion(Content content) => Convert.ToBase64String(content.RowVersion);

    private sealed class Repository : IImageStageRepository
    {
        private readonly Content _working;
        private readonly IReadOnlyList<MediaAsset> _media;
        private readonly bool _failSave;

        public Repository(
            Content durable,
            IReadOnlyList<MediaAsset> media,
            bool failSave = false,
            bool returnClone = false)
        {
            _working = returnClone ? Clone(durable) : durable;
            _media = media;
            _failSave = failSave;
        }

        public int SaveCount { get; private set; }
        public int RemovedLinkCount { get; private set; }

        public Task<ImageStageData?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult<ImageStageData?>(
                contentId == _working.Id ? new(_working, _media) : null);

        public void RemoveLinks(IReadOnlyCollection<ContentMediaLink> links) =>
            RemovedLinkCount += links.Count;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_failSave) throw new InvalidOperationException("synthetic image-stage save failure");
            SaveCount++;
            return Task.CompletedTask;
        }

        private static Content Clone(Content source)
        {
            var clone = new Content
            {
                Id = source.Id,
                CategoryId = source.CategoryId,
                Title = source.Title,
                CurrentWorkflowStep = source.CurrentWorkflowStep,
                RowVersion = [.. source.RowVersion]
            };
            foreach (var link in source.LinkedMedia)
            {
                clone.LinkedMedia.Add(new ContentMediaLink
                {
                    ContentId = clone.Id,
                    MediaAssetId = link.MediaAssetId
                });
            }
            return clone;
        }
    }
}
