using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of a feed analyses listing operation.
/// </summary>
public sealed record ListFeedAnalysesResult
{
    public required FeedAnalysisListItem[] Items { get; init; }
    public required PagingMetadata Paging { get; init; }
    public bool Success { get; init; }
    public FeedAnalysisError? Error { get; init; }
}

/// <summary>
/// Service-layer DTO representing a single feed analysis list item.
/// </summary>
public sealed record FeedAnalysisListItem(
    Guid AnalysisId,
    string TargetUrl,
    FeedAnalysisStatus Status,
    string[] Warnings,
    DateTimeOffset? AnalysisStartedAt,
    DateTimeOffset? AnalysisCompletedAt
);
