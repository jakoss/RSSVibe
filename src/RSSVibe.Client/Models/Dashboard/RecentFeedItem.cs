namespace RSSVibe.Client.Models.Dashboard;

/// <summary>
/// Combined item and feed information for recent activity display.
/// Aggregates data from both FeedItem and Feed entities for dashboard rendering.
/// </summary>
public sealed record RecentFeedItem(
    /// <summary>
    /// Item identifier.
    /// </summary>
    Guid ItemId,

    /// <summary>
    /// Article title.
    /// </summary>
    string Title,

    /// <summary>
    /// Article URL to open in browser.
    /// </summary>
    string Link,

    /// <summary>
    /// Publication timestamp from feed item (nullable).
    /// Falls back to DiscoveredAt if null.
    /// </summary>
    DateTimeOffset? PublishedAt,

    /// <summary>
    /// When item was discovered by feed parser.
    /// Used for sorting and fallback date display.
    /// </summary>
    DateTimeOffset DiscoveredAt,

    /// <summary>
    /// Name of parent feed for attribution.
    /// </summary>
    string FeedTitle,

    /// <summary>
    /// Parent feed identifier for navigation to feed detail.
    /// </summary>
    Guid FeedId
);
