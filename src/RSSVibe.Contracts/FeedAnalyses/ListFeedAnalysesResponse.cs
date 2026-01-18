namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Response with paginated list of feed analyses. Maps from FeedAnalysis entities.
/// </summary>
public sealed record ListFeedAnalysesResponse(
    FeedAnalysisListItemDto[] Items,
    PagingDto Paging
);
