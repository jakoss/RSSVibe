namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Preview item extracted from a feed analysis using current selectors.
/// </summary>
public sealed record FeedAnalysisPreviewItemDto(
    string Title,
    string Link,
    DateTimeOffset? PublishedAt,
    string? RawHtmlExcerpt
);
