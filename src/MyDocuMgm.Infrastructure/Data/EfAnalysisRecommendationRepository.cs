using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.AnalysisRecommendations;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfAnalysisRecommendationRepository(MyDocuMgmDbContext dbContext)
    : IAnalysisRecommendationRepository
{
    public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents
            .Include(value => value.ContentTags).ThenInclude(value => value.Tag)
            .Include(value => value.SourceEvidence)
            .SingleOrDefaultAsync(value => value.Id == contentId, cancellationToken);

    public Task<AnalysisRecommendationRun?> FindByIdempotencyKeyAsync(
        Guid contentId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        RunsWithItems()
            .SingleOrDefaultAsync(
                value => value.ContentId == contentId && value.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<AnalysisRecommendationRun?> FindLatestAsync(
        Guid contentId,
        CancellationToken cancellationToken) =>
        RunsWithItems()
            .Where(value => value.ContentId == contentId)
            .OrderByDescending(value => value.RequestedAtUtc)
            .ThenByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AnalysisRecommendationRun>> ListAsync(
        Guid contentId,
        int limit,
        CancellationToken cancellationToken) =>
        await RunsWithItems()
            .Where(value => value.ContentId == contentId)
            .OrderByDescending(value => value.RequestedAtUtc)
            .ThenByDescending(value => value.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<AnalysisRecommendationRun?> FindRunForDecisionAsync(
        Guid contentId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.AnalysisRecommendationRuns
            .Include(value => value.Items).ThenInclude(value => value.Evidence)
            .Include(value => value.Content).ThenInclude(value => value.ContentTags).ThenInclude(value => value.Tag)
            .SingleOrDefaultAsync(value => value.Id == runId && value.ContentId == contentId, cancellationToken);

    public Task AddRunAsync(AnalysisRecommendationRun run, CancellationToken cancellationToken) =>
        dbContext.AnalysisRecommendationRuns.AddAsync(run, cancellationToken).AsTask();

    public Task AddItemsAsync(
        IReadOnlyCollection<AnalysisRecommendationItem> items,
        CancellationToken cancellationToken) =>
        dbContext.AnalysisRecommendationItems.AddRangeAsync(items, cancellationToken);

    public Task<Tag?> FindTagAsync(string normalizedName, CancellationToken cancellationToken) =>
        dbContext.Tags.SingleOrDefaultAsync(value => value.NormalizedName == normalizedName, cancellationToken);

    public Task AddTagAsync(Tag tag, CancellationToken cancellationToken) =>
        dbContext.Tags.AddAsync(tag, cancellationToken).AsTask();

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException($"다른 변경이 먼저 저장되었습니다: {exception.Message}");
        }
    }

    private IQueryable<AnalysisRecommendationRun> RunsWithItems() =>
        dbContext.AnalysisRecommendationRuns
            .Include(value => value.Items)
            .ThenInclude(value => value.Evidence);
}
