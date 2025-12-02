namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Summary information for a feed in list view.
/// </summary>
public sealed record FeedListItemDto(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    int PendingParseCount,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RssUrl
);
