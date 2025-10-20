namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Detailed feed parse run response including resiliency metrics. Maps from FeedParseRun entity.
/// </summary>
public sealed record FeedParseRunDetailResponse(
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
    int SkippedItemsCount,
    int RetryCount,
    bool CircuitBreakerOpen,
    DateTimeOffset CreatedAt
);
