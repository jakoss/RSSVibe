namespace RSSVibe.Contracts.FeedItems;

/// <summary>
/// Detailed feed item response including raw metadata. Maps from FeedItem entity.
/// </summary>
public sealed record FeedItemDetailResponse(
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
    Guid? FirstParseRunId,
    Guid? LastParseRunId,
    FeedItemMetadataDto? RawMetadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
