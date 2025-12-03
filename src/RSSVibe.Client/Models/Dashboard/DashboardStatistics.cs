namespace RSSVibe.Client.Models.Dashboard;

/// <summary>
/// Aggregated metrics for statistic cards displayed on the dashboard.
/// All counts are calculated client-side from API responses.
/// </summary>
public sealed record DashboardStatistics(
    /// <summary>
    /// Count of all user's feeds.
    /// </summary>
    int TotalFeeds,

    /// <summary>
    /// Count of feeds with LastParseStatus == "succeeded".
    /// </summary>
    int ActiveFeeds,

    /// <summary>
    /// Count of feeds with LastParseStatus == "failed".
    /// </summary>
    int FailingFeeds,

    /// <summary>
    /// Count of analyses with Status == "pending" or (Status == "completed" and no approved feed).
    /// </summary>
    int PendingAnalyses
);
