using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of feed analysis creation operation.
/// </summary>
public sealed record CreateFeedAnalysisResult
{
    public bool Success { get; init; }
    public Guid AnalysisId { get; init; }
    public string? NormalizedUrl { get; init; }
    public string? Status { get; init; }
    public FeedPreflightChecks? PreflightChecks { get; init; }
    public FeedPreflightDetails? PreflightDetails { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public FeedAnalysisError? Error { get; init; }
    public string? ErrorDetail { get; init; }
}
