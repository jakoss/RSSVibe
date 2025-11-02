namespace RSSVibe.Contracts.FeedItems;

/// <summary>
/// Feed item metadata. Maps from FeedItemMetadata entity model stored in FeedItem.RawMetadata.
/// </summary>
public sealed record FeedItemMetadataDto(
    string? Author,
    string? Category,
    string[]? Tags,
    string? ImageUrl,
    string? ContentHash,
    int? WordCount,
    string? Language
);
