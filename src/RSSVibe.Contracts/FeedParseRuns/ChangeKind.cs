namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Represents the kind of change for a feed item in a parse run.
/// </summary>
public enum ChangeKind
{
    New,
    Refreshed,
    Unchanged
}
