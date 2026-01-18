using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Contracts.Feeds;

namespace RSSVibe.Client.Models.Dashboard;

/// <summary>
/// Aggregate container for all dashboard data fetched from APIs.
/// Combines feeds, analyses, recent items, and pre-calculated statistics.
/// </summary>
public sealed record DashboardData(
    /// <summary>
    /// All user's feeds used to calculate statistics and recent items.
    /// </summary>
    IReadOnlyList<FeedListItemDto> Feeds,

    /// <summary>
    /// All user's feed analyses (filtered to pending/completed for display).
    /// </summary>
    IReadOnlyList<FeedAnalysisListItemDto> Analyses,

    /// <summary>
    /// Recent feed items across all feeds for activity feed.
    /// </summary>
    IReadOnlyList<RecentFeedItem> RecentItems,

    /// <summary>
    /// Pre-calculated aggregated metrics for statistic cards.
    /// </summary>
    DashboardStatistics Statistics
);
