namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Detailed feed analysis response including selectors, preflight checks, and warnings.
/// </summary>
public sealed record FeedAnalysisDetailResponse(
    Guid AnalysisId,
    string TargetUrl,
    string NormalizedUrl,
    FeedAnalysisStatus Status,
    string[] PreflightChecks,
    FeedPreflightDetails PreflightDetails,
    FeedSelectors? Selectors,
    string[] Warnings,
    string? AiModel,
    Guid? ApprovedFeedId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
