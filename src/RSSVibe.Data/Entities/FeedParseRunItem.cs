namespace RSSVibe.Data.Entities;

public sealed class FeedParseRunItem
{
    public Guid FeedParseRunId { get; init; }
    public Guid FeedItemId { get; init; }
    public FeedParseItemChange ChangeKind { get; set; }
    public DateTimeOffset SeenAt { get; init; }

    // Navigation properties
    public FeedParseRun FeedParseRun { get; set; } = null!;
    public FeedItem FeedItem { get; set; } = null!;
}
