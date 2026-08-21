using MyDocuMgm.Application.AnalysisRecommendations;

namespace MyDocuMgm.Infrastructure;

public sealed class UnavailableAnalysisRecommendationProvider : IAnalysisRecommendationProvider
{
    public string ProviderIdentifier => "NOT_CONNECTED";

    public Task<AnalysisRecommendationProviderResult> RecommendAsync(
        AnalysisRecommendationProviderInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new AnalysisRecommendationException(
            AnalysisRecommendationFailureKind.UNAVAILABLE,
            "ANALYSIS_PROVIDER_NOT_CONNECTED",
            "추천 기능이 아직 연결되지 않았습니다. 직접 작성으로 계속할 수 있습니다.");
    }
}
