using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response with paginated list of feeds. Maps from Feed entities.
/// </summary>
public sealed record ListFeedsResponse(
    FeedListItemDto[] Items,
    PagingDto Paging
);
