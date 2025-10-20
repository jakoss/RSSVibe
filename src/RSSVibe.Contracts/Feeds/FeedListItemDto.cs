namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Feed item for list responses. Subset of Feed entity data.
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
    DateTimeOffset? LastModified,
    DateTimeOffset? LastParsedAt,
    DateTimeOffset? NextParseAfter,
    string? LastParseStatus,
    int PendingParseCount,
    Guid? AnalysisId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RssUrl
);
