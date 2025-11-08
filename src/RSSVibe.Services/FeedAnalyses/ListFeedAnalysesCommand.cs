using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command for listing feed analyses for a specific user with optional filtering,
/// sorting, pagination and search capabilities.
/// </summary>
public sealed record ListFeedAnalysesCommand(
    Guid UserId,
    FeedAnalysisStatus? Status,
    string? Sort,
    int Skip,
    int Take,
    string? Search
);
