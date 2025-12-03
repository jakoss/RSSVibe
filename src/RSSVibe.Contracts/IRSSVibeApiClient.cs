namespace RSSVibe.Contracts;

/// <summary>
/// Type-safe HTTP client for RSSVibe API endpoints.
/// Provides strongly-typed methods for all API v1 endpoints.
/// </summary>
public interface IRSSVibeApiClient
{
    /// <summary>
    /// Auth endpoints under /api/v1/auth
    /// </summary>
    IAuthClient Auth { get; }

    /// <summary>
    /// Feed analysis endpoints under /api/v1/feed-analyses
    /// </summary>
    IFeedAnalysesClient FeedAnalyses { get; }

    /// <summary>
    /// Feed endpoints under /api/v1/feeds
    /// </summary>
    IFeedsClient Feeds { get; }
}
