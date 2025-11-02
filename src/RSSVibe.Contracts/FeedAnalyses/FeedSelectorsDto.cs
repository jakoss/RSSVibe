namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// CSS selectors for extracting feed data. Maps from FeedSelectors entity model.
/// </summary>
public sealed record FeedSelectorsDto(
    string ItemContainer,
    string Title,
    string Link,
    string? Description,
    string? PublishedDate,
    string? Author,
    string? Image
);
