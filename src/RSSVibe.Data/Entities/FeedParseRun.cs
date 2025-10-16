using RSSVibe.Data.Models;

namespace RSSVibe.Data.Entities;

public sealed class FeedParseRun
{
    public Guid Id { get; init; }
    public Guid FeedId { get; init; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public FeedParseStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public short? HttpStatusCode { get; set; }
    public required HttpResponseHeaders ResponseHeaders { get; set; }
    public int FetchedItemsCount { get; set; }
    public int NewItemsCount { get; set; }
    public int UpdatedItemsCount { get; set; }
    public int SkippedItemsCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; }

    // Navigation properties
    public Feed Feed { get; set; } = null!;
    public ICollection<FeedParseRunItem> ParseRunItems { get; set; } = [];
}
