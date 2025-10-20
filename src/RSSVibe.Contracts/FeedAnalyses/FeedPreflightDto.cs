namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Preflight check status and details for feed analysis.
/// Maps from FeedAnalysis.PreflightChecks (flags enum) and FeedAnalysis.PreflightDetails (JSON model).
/// </summary>
public sealed record FeedPreflightDto(
    string[] Checks,
    Dictionary<string, object>? Details
);
