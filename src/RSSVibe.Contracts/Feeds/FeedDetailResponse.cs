using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Detailed feed response including selectors and cache metadata. Maps from Feed entity.
/// </summary>
public sealed record FeedDetailResponse(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    FeedSelectorsDto Selectors,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    string? Etag,
    DateTimeOffset? LastModified,
    DateTimeOffset? LastParsedAt,
    DateTimeOffset? NextParseAfter,
    string? LastParseStatus,
    Guid? AnalysisId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RssUrl,
    CacheHeadersDto CacheHeaders
);
