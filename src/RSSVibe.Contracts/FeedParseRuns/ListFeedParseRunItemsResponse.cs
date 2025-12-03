namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Response with paginated list of items from a parse run. Maps from FeedParseRunItem and FeedItem entities.
/// </summary>
public sealed record ListFeedParseRunItemsResponse(
    FeedParseRunItemDto[] Items,
    RSSVibe.Contracts.PagingDto Paging
);
