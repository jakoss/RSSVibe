namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Standard pagination metadata for list responses.
/// </summary>
public sealed record PagingDto(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);
