namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Paging metadata for list responses.
/// </summary>
public sealed record PagingMetadata(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);
