namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Preflight check results and metadata for feed analysis.
/// </summary>
public sealed record FeedPreflightDetails(
    bool RequiresJavascript,
    bool RequiresAuthentication,
    bool IsPaywalled,
    bool HasInvalidMarkup,
    bool IsRateLimited,
    string? ErrorMessage,
    string AdditionalInfo
);
