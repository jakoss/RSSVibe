namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Response from initiating feed analysis. Maps from FeedAnalysis entity.
/// </summary>
public sealed record CreateFeedAnalysisResponse(
    Guid AnalysisId,
    string Status,
    string NormalizedUrl,
    FeedPreflightDto Preflight,
    DateTimeOffset CreatedAt
);
