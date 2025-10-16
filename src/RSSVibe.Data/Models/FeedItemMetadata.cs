namespace RSSVibe.Data.Models;

public sealed class FeedItemMetadata
{
    public string? Author { get; init; }
    public string? Category { get; init; }
    public string[]? Tags { get; init; }
    public string? ImageUrl { get; init; }
    public string? ContentHash { get; init; }
    public int? WordCount { get; init; }
    public string? Language { get; init; }
    public Dictionary<string, string>? CustomFields { get; init; }
}
