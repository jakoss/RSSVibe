namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of a feed analysis deletion operation.
/// </summary>
public sealed record DeleteFeedAnalysisResult
{
    public bool Success { get; init; }
    public FeedAnalysisError? Error { get; init; }
    public string? ErrorDetail { get; init; }

    public static DeleteFeedAnalysisResult Succeeded()
    {
        return new() { Success = true };
    }

    public static DeleteFeedAnalysisResult Failed(FeedAnalysisError error, string? detail = null)
    {
        return new() { Success = false, Error = error, ErrorDetail = detail };
    }
}
