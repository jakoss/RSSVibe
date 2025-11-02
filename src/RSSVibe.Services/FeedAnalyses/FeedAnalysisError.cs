namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Errors that can occur during feed analysis operations.
/// </summary>
public enum FeedAnalysisError
{
    /// <summary>
    /// A feed analysis with the same normalized URL already exists for this user.
    /// </summary>
    DuplicateAnalysis,

    /// <summary>
    /// Force reanalysis requested but cooldown period has not elapsed.
    /// </summary>
    ReanalysisCooldown,

    /// <summary>
    /// OpenRouter AI service is unavailable or misconfigured.
    /// </summary>
    AiServiceUnavailable,

    /// <summary>
    /// Database or persistence layer error.
    /// </summary>
    DatabaseError,

    /// <summary>
    /// URL normalization or validation failed.
    /// </summary>
    InvalidUrl,

    /// <summary>
    /// Target URL points to internal/private network (SSRF protection).
    /// </summary>
    ForbiddenUrl,

    /// <summary>
    /// Preflight checks failed critically (unreachable, requires authentication).
    /// </summary>
    PreflightFailed
}
