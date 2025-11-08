namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// CSS selectors for extracting feed data from HTML.
/// </summary>
public sealed record FeedSelectors(
    string? List,
    string? Item,
    string? Title,
    string? Link,
    string? Published,
    string? Summary
);
