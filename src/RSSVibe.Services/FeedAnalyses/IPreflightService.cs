using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service for performing preflight checks on target URLs.
/// </summary>
public interface IPreflightService
{
    /// <summary>
    /// Performs synchronous preflight checks on a URL to detect potential issues.
    /// </summary>
    /// <param name="targetUrl">The URL to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preflight check results including detected issues and details.</returns>
    Task<PreflightCheckResult> PerformPreflightChecksAsync(
        string targetUrl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of preflight checks.
/// </summary>
public sealed record PreflightCheckResult(
    FeedPreflightChecks Checks,
    FeedPreflightDetails Details,
    string[] Warnings,
    bool IsCriticalFailure
);
