namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response from creating a feed. Maps from Feed entity.
/// </summary>
public sealed record CreateFeedResponse(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string Title,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    DateTimeOffset? LastParsedAt,
    DateTimeOffset? NextParseAfter,
    string RssUrl
);
