using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Contracts;

/// <summary>
/// Type-safe client for feed analysis endpoints.
/// </summary>
public interface IFeedAnalysesClient
{
    /// <summary>
    /// POST /api/v1/feed-analyses - Create a new feed analysis.
    /// </summary>
    Task<ApiResult<CreateFeedAnalysisResponse>> CreateAsync(
        CreateFeedAnalysisRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/feed-analyses - List feed analyses for current user.
    /// </summary>
    Task<ApiResult<ListFeedAnalysesResponse>> ListAsync(
        ListFeedAnalysesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/feed-analyses/{analysisId} - Get feed analysis details.
    /// </summary>
    Task<ApiResult<FeedAnalysisDetailResponse>> GetAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);
}
