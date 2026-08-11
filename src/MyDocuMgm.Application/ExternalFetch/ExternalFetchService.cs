using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.ExternalFetch;

public sealed class ExternalFetchService(
    IExternalFetchRepository repository,
    IExternalPageFetcher pageFetcher,
    IHtmlContentExtractor htmlExtractor,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan RetryWindow = TimeSpan.FromMinutes(10);

    public async Task<ExternalFetchAttemptDto> StartAsync(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var content = await FindContentAsync(contentId, cancellationToken);
        EnsureFetchable(content);
        var attempt = await repository.CreateAttemptAsync(
            content,
            timeProvider.GetUtcNow().UtcDateTime - RetryWindow,
            cancellationToken);

        try
        {
            var page = await pageFetcher.FetchAsync(new Uri(content.NormalizedUrl!), cancellationToken);
            var extracted = htmlExtractor.Extract(page.Html, new Uri(page.FinalUrl));
            var body = extracted.Body.Trim();
            if (body.Length == 0)
            {
                throw Error(
                    "FETCH_CONTENT_NOT_FOUND",
                    "일반 공개 HTML URL만 가져올 수 있습니다. Instagram은 수동 입력을 사용하세요.",
                    ExternalFetchFailureKind.REMOTE);
            }

            if (body.Length > ExternalFetchAttempt.ExtractedTextMaxLength)
            {
                body = body[..ExternalFetchAttempt.ExtractedTextMaxLength];
            }

            attempt.Succeed(
                page.FinalUrl,
                page.HttpStatusCode,
                page.ResponseMimeType,
                page.ResponseBytes,
                page.ContentSha256,
                page.ETag,
                page.LastModifiedAtUtc,
                Limit(extracted.Title, 300),
                Limit(extracted.Description, 2_000),
                Limit(extracted.AuthorName, 300),
                extracted.PublishedAtUtc,
                body);
            await repository.SaveChangesAsync(cancellationToken);
            return ExternalFetchMappings.ToDto(attempt);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            attempt.Cancel("FETCH_CANCELLED", "가져오기 요청이 취소되었습니다.");
            await repository.SaveChangesAsync(CancellationToken.None);
            throw Error(
                "FETCH_CANCELLED",
                "가져오기 요청이 취소되었습니다.",
                ExternalFetchFailureKind.TIMEOUT,
                exception);
        }
        catch (ExternalFetchException exception)
        {
            attempt.Fail(exception.Code, exception.Message);
            await repository.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            const string message = "외부 문서를 가져오지 못했습니다. 잠시 후 다시 시도하세요.";
            attempt.Fail("FETCH_PROCESSING_FAILED", message);
            await repository.SaveChangesAsync(CancellationToken.None);
            throw Error(
                "FETCH_PROCESSING_FAILED",
                message,
                ExternalFetchFailureKind.REMOTE,
                exception);
        }
    }

    public async Task<ExternalFetchAttemptDto> GetAsync(
        Guid contentId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptAsync(contentId, attemptId, cancellationToken)
            ?? throw new NotFoundException("가져오기 기록을 찾을 수 없습니다.");
        return ExternalFetchMappings.ToDto(attempt);
    }

    public async Task<ExternalFetchAttemptDto> GetLatestAsync(
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var attempt = await repository.FindLatestAttemptAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("가져오기 기록을 찾을 수 없습니다.");
        return ExternalFetchMappings.ToDto(attempt);
    }

    public async Task<ExternalFetchApplyDto> ApplyAsync(
        Guid contentId,
        Guid attemptId,
        ApplyExternalFetchRequest request,
        CancellationToken cancellationToken)
    {
        var content = await FindContentAsync(contentId, cancellationToken);
        EnsureFetchable(content);
        var attempt = await repository.FindAttemptAsync(contentId, attemptId, cancellationToken)
            ?? throw new NotFoundException("가져오기 기록을 찾을 수 없습니다.");

        if (attempt.Status != ExternalFetchStatus.APPLIED)
        {
            content.ApplyExternalFetch(request.Title, request.Description, request.Body);
            attempt.MarkApplied();
            await repository.ApplyAsync(
                content,
                attempt,
                new SourceEvidence
                {
                    ContentId = content.Id,
                    Content = content,
                    SourceType = "PUBLIC_HTML",
                    SourceTitle = Limit(request.Title, 300),
                    SourceReference = Limit(attempt.FinalUrl, 2_000),
                    CapturedAtUtc = attempt.CompletedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime
                },
                cancellationToken);
        }

        return new(
            ExternalFetchMappings.ToDto(attempt),
            ExternalFetchMappings.ToIntakeDto(content));
    }

    private async Task<Content> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
        await repository.FindContentAsync(contentId, cancellationToken)
            ?? throw new NotFoundException("URL 접수 정보를 찾을 수 없습니다.");

    private static void EnsureFetchable(Content content)
    {
        if (content.CurrentWorkflowStep != WorkflowStep.URL)
        {
            throw new DomainRuleException(
                "URL_STAGE_ALREADY_COMPLETED",
                "URL intake data cannot be changed after the workflow leaves the URL stage.");
        }

        if (content.SourceKind != ContentSourceKind.GENERIC || string.IsNullOrWhiteSpace(content.NormalizedUrl))
        {
            throw new DomainRuleException(
                "EXTERNAL_FETCH_GENERIC_URL_REQUIRED",
                "현재 접수 정보의 URL과 다른 결과는 적용할 수 없습니다.");
        }
    }

    private static string? Limit(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static ExternalFetchException Error(
        string code,
        string message,
        ExternalFetchFailureKind kind,
        Exception? innerException = null) => new(code, message, kind, innerException);
}
