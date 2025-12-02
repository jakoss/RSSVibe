using RSSVibe.Contracts;

namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Response with paginated list of feed parse runs. Maps from FeedParseRun entities.
/// </summary>
public sealed record ListFeedParseRunsResponse(
    FeedParseRunDto[] Runs,
    RSSVibe.Contracts.PagingDto Paging
);
