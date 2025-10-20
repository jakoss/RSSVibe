namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Detailed feed analysis response. Maps from FeedAnalysis entity with full selectors and metadata.
/// </summary>
public sealed record FeedAnalysisDetailResponse(
    Guid AnalysisId,
    string TargetUrl,
    string NormalizedUrl,
    string Status,
    string[] PreflightChecks,
    Dictionary<string, object>? PreflightDetails,
    FeedSelectorsDto? Selectors,
    string[] Warnings,
    string? AiModel,
    Guid? ApprovedFeedId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
