using RSSVibe.Data.Entities;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of a feed analysis retrieval operation.
/// </summary>
public sealed record GetFeedAnalysisResult
{
    public bool Success { get; init; }
    public FeedAnalysis? Analysis { get; init; }
    public FeedAnalysisError? Error { get; init; }

    public static GetFeedAnalysisResult Succeeded(FeedAnalysis analysis)
    {
        return new() { Success = true, Analysis = analysis };
    }

    public static GetFeedAnalysisResult Failed(FeedAnalysisError error)
    {
        return new() { Success = false, Error = error };
    }
}
