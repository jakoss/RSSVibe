using RSSVibe.Data.Abstractions;
using RSSVibe.Data.Models;

namespace RSSVibe.Data.Entities;

public sealed class FeedAnalysis : IAuditableEntity
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string TargetUrl { get; set; }
    public required string NormalizedUrl { get; set; }
    public FeedAnalysisStatus AnalysisStatus { get; set; }
    public FeedPreflightChecks PreflightChecks { get; set; }
    public required FeedPreflightDetails PreflightDetails { get; set; }
    public FeedSelectors? Selectors { get; set; }
    public string[] Warnings { get; set; } = [];
    public string? AiModel { get; set; }
    public Guid? ApprovedFeedId { get; set; }
    public DateTimeOffset? AnalysisStartedAt { get; set; }
    public DateTimeOffset? AnalysisCompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public Feed? ApprovedFeed { get; set; }
}
