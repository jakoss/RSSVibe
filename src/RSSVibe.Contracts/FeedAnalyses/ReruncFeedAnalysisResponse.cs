namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Response from re-enqueueing a feed analysis. Maps from updated FeedAnalysis entity.
/// </summary>
public sealed record ReruncFeedAnalysisResponse(
    Guid AnalysisId,
    string Status
);
