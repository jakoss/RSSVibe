namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command for deleting (cancelling) a feed analysis.
/// Includes user context for ownership verification.
/// </summary>
public sealed record DeleteFeedAnalysisCommand(
    Guid AnalysisId,
    Guid UserId
);
