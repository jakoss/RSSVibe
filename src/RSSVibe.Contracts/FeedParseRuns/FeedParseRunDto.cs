namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Feed parse run data. Maps from FeedParseRun entity.
/// </summary>
public sealed record FeedParseRunDto(
    Guid ParseRunId,
    Guid FeedId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string? FailureReason,
    short? HttpStatusCode,
    HttpResponseHeadersDto? ResponseHeaders,
    int FetchedItemsCount,
    int NewItemsCount,
    int UpdatedItemsCount,
    int SkippedItemsCount
);
