namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response containing paginated list of feeds.
/// </summary>
public sealed record ListFeedsResponse(
    IReadOnlyList<FeedListItemDto> Items,
    PagingDto Paging
);
