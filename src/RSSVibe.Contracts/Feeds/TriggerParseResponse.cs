namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response from manually triggering a feed parse. Maps to FeedParseRun creation.
/// </summary>
public sealed record TriggerParseResponse(
    Guid FeedId,
    Guid ParseRunId,
    string Status
);
