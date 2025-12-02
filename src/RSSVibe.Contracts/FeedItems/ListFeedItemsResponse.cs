using RSSVibe.Contracts;

namespace RSSVibe.Contracts.FeedItems;

/// <summary>
/// Response with paginated list of feed items. Maps from FeedItem entities.
/// </summary>
public sealed record ListFeedItemsResponse(
    FeedItemDto[] Items,
    RSSVibe.Contracts.PagingDto Paging
);
