namespace RSSVibe.Data.Models;

public sealed class FeedSelectors
{
    public required string ItemContainer { get; init; }
    public required string Title { get; init; }
    public required string Link { get; init; }
    public string? Description { get; init; }
    public string? PublishedDate { get; init; }
    public string? Author { get; init; }
    public string? Image { get; init; }
    public Dictionary<string, string>? CustomSelectors { get; init; }
}
