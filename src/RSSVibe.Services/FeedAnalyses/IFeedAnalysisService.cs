namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service for feed analysis operations including AI-powered selector detection.
/// </summary>
public interface IFeedAnalysisService
{
    /// <summary>
    /// Creates a new feed analysis request with synchronous preflight checks,
    /// then enqueues background AI processing for selector detection.
    /// </summary>
    /// <param name="command">Command containing target URL and analysis options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing analysis details with initial preflight results or error information.</returns>
    Task<CreateFeedAnalysisResult> CreateFeedAnalysisAsync(
        CreateFeedAnalysisCommand command,
        CancellationToken cancellationToken = default);
}
