namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Feed parse run item data. Maps from FeedParseRunItem join table with FeedItem details.
/// </summary>
public sealed record FeedParseRunItemDto(
    Guid ItemId,
    Guid FeedId,
    string ChangeKind,
    DateTimeOffset SeenAt,
    string Title,
    string Link
);
