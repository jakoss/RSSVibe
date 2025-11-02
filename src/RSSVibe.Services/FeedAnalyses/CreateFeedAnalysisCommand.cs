namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command to create a new feed analysis.
/// </summary>
public sealed record CreateFeedAnalysisCommand(
    Guid UserId,
    string TargetUrl,
    string? AiModel,
    bool ForceReanalysis
);
