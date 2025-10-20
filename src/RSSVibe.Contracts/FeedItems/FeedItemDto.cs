namespace RSSVibe.Contracts.FeedItems;

/// <summary>
/// Feed item data. Maps from FeedItem entity.
/// </summary>
public sealed record FeedItemDto(
    Guid ItemId,
    Guid FeedId,
    string Title,
    string? Summary,
    string Link,
    string SourceUrl,
    string NormalizedSourceUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset LastSeenAt,
    string? ChangeKind
);
