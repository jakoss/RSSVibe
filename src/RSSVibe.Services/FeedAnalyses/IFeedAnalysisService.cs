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

    Task<ListFeedAnalysesResult> ListFeedAnalysesAsync(
        ListFeedAnalysesCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific feed analysis by ID with ownership verification.
    /// </summary>
    /// <param name="command">Command containing analysis ID and user ID for ownership check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the analysis entity or error information.</returns>
    Task<GetFeedAnalysisResult> GetFeedAnalysisAsync(
        GetFeedAnalysisCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes (cancels) a feed analysis. Only analyses in pending or inProgress status can be deleted.
    /// Completed, failed, or superseded analyses are preserved as historical records.
    /// </summary>
    /// <param name="command">Command containing analysis ID and user ID for ownership verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or specific error.</returns>
    Task<DeleteFeedAnalysisResult> DeleteFeedAnalysisAsync(
        DeleteFeedAnalysisCommand command,
        CancellationToken cancellationToken = default);
}
