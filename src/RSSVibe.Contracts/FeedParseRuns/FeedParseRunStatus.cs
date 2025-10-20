namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Represents the status of a feed parse run.
/// </summary>
public enum FeedParseRunStatus
{
    Scheduled,
    Running,
    Succeeded,
    Failed,
    Skipped
}
