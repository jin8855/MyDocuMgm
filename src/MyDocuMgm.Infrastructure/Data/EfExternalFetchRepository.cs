using System.Data;
using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.ExternalFetch;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfExternalFetchRepository(MyDocuMgmDbContext dbContext) : IExternalFetchRepository
{
    public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents
            .Include(content => content.LinkedMedia)
            .ThenInclude(link => link.MediaAsset)
            .SingleOrDefaultAsync(content => content.Id == contentId, cancellationToken);

    public async Task<ExternalFetchAttempt> CreateAttemptAsync(
        Content content,
        DateTime recentCutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [Id] FROM [dbo].[Contents] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {content.Id}",
            cancellationToken);
        var recentCount = await dbContext.ExternalFetchAttempts
            .CountAsync(
                attempt => attempt.ContentId == content.Id && attempt.StartedAtUtc >= recentCutoffUtc,
                cancellationToken);
        if (recentCount >= 3)
        {
            throw new ExternalFetchException(
                "FETCH_RETRY_LIMIT",
                "10분 동안 같은 콘텐츠에서 최대 3회까지 가져올 수 있습니다.",
                ExternalFetchFailureKind.RETRY_LIMIT);
        }

        var lastAttemptNumber = await dbContext.ExternalFetchAttempts
            .Where(attempt => attempt.ContentId == content.Id)
            .Select(attempt => (int?)attempt.AttemptNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var attempt = new ExternalFetchAttempt
        {
            ContentId = content.Id,
            Content = content,
            AttemptNumber = lastAttemptNumber + 1,
            StartedAtUtc = DateTime.UtcNow
        };
        dbContext.ExternalFetchAttempts.Add(attempt);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return attempt;
    }

    public Task<ExternalFetchAttempt?> FindAttemptAsync(
        Guid contentId,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        dbContext.ExternalFetchAttempts.SingleOrDefaultAsync(
            attempt => attempt.Id == attemptId && attempt.ContentId == contentId,
            cancellationToken);

    public Task<ExternalFetchAttempt?> FindLatestAttemptAsync(
        Guid contentId,
        CancellationToken cancellationToken) =>
        dbContext.ExternalFetchAttempts
            .AsNoTracking()
            .Where(attempt => attempt.ContentId == contentId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

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

    public async Task ApplyAsync(
        Content content,
        ExternalFetchAttempt attempt,
        SourceEvidence evidence,
        CancellationToken cancellationToken)
    {
        dbContext.SourceEvidence.Add(evidence);
        await SaveChangesAsync(cancellationToken);
    }
}
