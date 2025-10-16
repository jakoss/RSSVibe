using RSSVibe.Data.Abstractions;
using RSSVibe.Data.Models;

namespace RSSVibe.Data.Entities;

public sealed class Feed : IAuditableEntity
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string SourceUrl { get; set; }
    public required string NormalizedSourceUrl { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Language { get; set; }
    public required FeedSelectors Selectors { get; set; }
    public FeedUpdateUnit UpdateIntervalUnit { get; set; }
    public short UpdateIntervalValue { get; set; }
    public short TtlMinutes { get; set; } = 60;
    public string? Etag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public DateTimeOffset? LastParsedAt { get; set; }
    public DateTimeOffset? NextParseAfter { get; set; }
    public FeedParseStatus? LastParseStatus { get; set; }
    public Guid? AnalysisId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public FeedAnalysis? Analysis { get; set; }
    public ICollection<FeedParseRun> ParseRuns { get; set; } = [];
    public ICollection<FeedItem> Items { get; set; } = [];
}
