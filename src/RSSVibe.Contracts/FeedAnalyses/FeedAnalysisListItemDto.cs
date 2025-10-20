namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Feed analysis item for list responses. Subset of FeedAnalysis entity data.
/// </summary>
public sealed record FeedAnalysisListItemDto(
    Guid AnalysisId,
    string TargetUrl,
    string Status,
    string[] Warnings,
    DateTimeOffset? AnalysisStartedAt,
    DateTimeOffset? AnalysisCompletedAt
);
