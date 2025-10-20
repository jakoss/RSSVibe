namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Represents the status of a feed analysis.
/// </summary>
public enum FeedAnalysisStatus
{
    Pending,
    Completed,
    Failed,
    Superseded
}
