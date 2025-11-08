namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command for retrieving a specific feed analysis.
/// </summary>
public sealed record GetFeedAnalysisCommand(
    Guid AnalysisId,
    Guid UserId
);
