using RSSVibe.Data.Abstractions;
using RSSVibe.Data.Models;

namespace RSSVibe.Data.Entities;

public sealed class FeedItem : IAuditableEntity
{
    public Guid Id { get; init; }
    public Guid FeedId { get; init; }
    public required string Fingerprint { get; set; }
    public required string SourceUrl { get; set; }
    public required string NormalizedSourceUrl { get; set; }
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
    public Guid? FirstParseRunId { get; set; }
    public Guid? LastParseRunId { get; set; }
    public required FeedItemMetadata RawMetadata { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public Feed Feed { get; set; } = null!;
    public FeedParseRun? FirstParseRun { get; set; }
    public FeedParseRun? LastParseRun { get; set; }
    public ICollection<FeedParseRunItem> ParseRunItems { get; set; } = [];
}
