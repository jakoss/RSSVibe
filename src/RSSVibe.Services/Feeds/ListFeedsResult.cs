namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of listing feeds.
/// </summary>
public sealed record ListFeedsResult
{
    public bool Success { get; init; }
    public IReadOnlyList<FeedListItem>? Items { get; init; }
    public int TotalCount { get; init; }
    public FeedListError? Error { get; init; }
}

/// <summary>
/// Feed summary data for list view.
/// </summary>
public sealed record FeedListItem(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    string UpdateIntervalUnit,
    short UpdateIntervalValue,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    int PendingParseCount,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);