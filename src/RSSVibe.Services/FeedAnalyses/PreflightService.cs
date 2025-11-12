using Microsoft.Extensions.Logging;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;
using System.Net;
using System.Text.Json;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service for performing preflight checks on target URLs.
/// </summary>
internal sealed partial class PreflightService(
    IHttpClientFactory httpClientFactory,
    ILogger<PreflightService> logger) : IPreflightService
{
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(10);

    public async Task<PreflightCheckResult> PerformPreflightChecksAsync(
        string targetUrl,
        CancellationToken cancellationToken = default)
    {
        var checks = FeedPreflightChecks.None;
        var warnings = new List<string>();
        var details = new Dictionary<string, string>();
        var isCriticalFailure = false;

        try
        {
            using var httpClient = httpClientFactory.CreateClient("PreflightClient");
            httpClient.Timeout = _requestTimeout;

            // Perform HTTP request
            var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.Add("User-Agent", "RSSVibe/1.0 (+https://rssvibe.com/bot)");

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

#pragma warning disable CA1305
            details["statusCode"] = ((int)response.StatusCode).ToString();
#pragma warning restore CA1305
            details["contentType"] = response.Content.Headers.ContentType?.ToString() ?? "unknown";

            // Check status code
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                checks |= FeedPreflightChecks.RequiresAuthentication;
                warnings.Add("Target URL requires authentication");
                isCriticalFailure = true; // Cannot proceed without auth
                logger.LogWarning("Preflight check: URL requires authentication - {Url}", targetUrl);
            }
            else if (!response.IsSuccessStatusCode)
            {
                checks |= FeedPreflightChecks.UnknownIssue;
                warnings.Add($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                isCriticalFailure = true; // Cannot proceed with non-2xx
                logger.LogWarning("Preflight check: HTTP error {StatusCode} - {Url}", response.StatusCode, targetUrl);
            }
            else
            {
                // Read content for deeper checks
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                // Check for JavaScript requirements
                if (DetectJavaScriptRequirement(content))
                {
                    checks |= FeedPreflightChecks.RequiresJavascript;
                    warnings.Add("Target URL may require JavaScript rendering");
                    logger.LogInformation("Preflight check: JavaScript detected - {Url}", targetUrl);
                }

                // Check for paywall indicators
                if (DetectPaywall(content))
                {
                    checks |= FeedPreflightChecks.Paywalled;
                    warnings.Add("Target URL may have a paywall");
                    logger.LogInformation("Preflight check: Paywall detected - {Url}", targetUrl);
                }

                // Check for invalid markup
                if (!ValidateHtmlMarkup(content))
                {
                    checks |= FeedPreflightChecks.InvalidMarkup;
                    warnings.Add("Target URL has invalid HTML markup");
                    logger.LogInformation("Preflight check: Invalid markup - {Url}", targetUrl);
                }

#pragma warning disable CA1305
                details["contentLength"] = content.Length.ToString();
#pragma warning restore CA1305
            }

            // Check for rate limiting
            if (response.StatusCode == (HttpStatusCode)429 ||
                response.Headers.Contains("X-RateLimit-Remaining"))
            {
                checks |= FeedPreflightChecks.RateLimited;
                warnings.Add("Target URL has rate limiting");
                logger.LogInformation("Preflight check: Rate limiting detected - {Url}", targetUrl);
            }
        }
        catch (HttpRequestException ex)
        {
            checks |= FeedPreflightChecks.UnknownIssue;
            warnings.Add($"Failed to connect: {ex.Message}");
            isCriticalFailure = true;
            logger.LogError(ex, "Preflight check: HTTP request failed - {Url}", targetUrl);
        }
        catch (TaskCanceledException)
        {
            checks |= FeedPreflightChecks.UnknownIssue;
            warnings.Add("Request timeout");
            isCriticalFailure = true;
            logger.LogWarning("Preflight check: Request timeout - {Url}", targetUrl);
        }
        catch (Exception ex)
        {
            checks |= FeedPreflightChecks.UnknownIssue;
            warnings.Add($"Unexpected error: {ex.Message}");
            isCriticalFailure = true;
            logger.LogError(ex, "Preflight check: Unexpected error - {Url}", targetUrl);
        }

        var preflightDetails = new FeedPreflightDetails
        {
            RequiresJavascript = checks.HasFlag(FeedPreflightChecks.RequiresJavascript),
            RequiresAuthentication = checks.HasFlag(FeedPreflightChecks.RequiresAuthentication),
            IsPaywalled = checks.HasFlag(FeedPreflightChecks.Paywalled),
            HasInvalidMarkup = checks.HasFlag(FeedPreflightChecks.InvalidMarkup),
            IsRateLimited = checks.HasFlag(FeedPreflightChecks.RateLimited),
            ErrorMessage = isCriticalFailure ? warnings.FirstOrDefault() : null,
            AdditionalInfo = JsonSerializer.Serialize(details)
        };

        return new PreflightCheckResult(checks, preflightDetails, [.. warnings], isCriticalFailure);
    }

    private static bool DetectJavaScriptRequirement(string htmlContent)
    {
        // Check for SPA frameworks
        if (htmlContent.Contains("react", StringComparison.OrdinalIgnoreCase) ||
            htmlContent.Contains("vue", StringComparison.OrdinalIgnoreCase) ||
            htmlContent.Contains("angular", StringComparison.OrdinalIgnoreCase) ||
            htmlContent.Contains("__NEXT_DATA__", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for <noscript> warnings
        if (htmlContent.Contains("<noscript>", StringComparison.OrdinalIgnoreCase))
        {
            var noscriptContent = htmlContent[
                htmlContent.IndexOf("<noscript>", StringComparison.OrdinalIgnoreCase)..];
            if (noscriptContent.Contains("enable JavaScript", StringComparison.OrdinalIgnoreCase) ||
                noscriptContent.Contains("requires JavaScript", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for minimal body content (likely SPA)
        var bodyStart = htmlContent.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var bodyEnd = htmlContent.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyStart > -1 && bodyEnd > -1)
        {
            var bodyContent = htmlContent[bodyStart..bodyEnd];
            // If body has very little content but many scripts, likely SPA
            var scriptCount = ScriptCountRegex().Count(bodyContent);
            var textContent = ExtractTextContentRegex().Replace(bodyContent, "").Trim();
            if (scriptCount > 3 && textContent.Length < 200)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DetectPaywall(string htmlContent)
    {
        var paywallIndicators = new[]
        {
            "paywall",
            "subscription required",
            "subscribe to read",
            "premium content",
            "members only",
            "article limit reached",
            "free articles remaining"
        };

        return paywallIndicators.Any(indicator =>
            htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateHtmlMarkup(string htmlContent)
    {
        // Basic HTML validation: check for balanced tags
        try
        {
            // Check for basic HTML structure
            if (!htmlContent.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                !htmlContent.Contains("</html>", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check for major structural issues (could use HtmlAgilityPack for deeper validation)
            var openTags = System.Text.RegularExpressions.Regex.Matches(htmlContent, "<(?!/)(?!!)[^>]+>");
            var closeTags = System.Text.RegularExpressions.Regex.Matches(htmlContent, "</[^>]+>");

            // If significantly imbalanced, likely invalid
            if (Math.Abs(openTags.Count - closeTags.Count) > openTags.Count * 0.3)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]+>")]
    private static partial System.Text.RegularExpressions.Regex ExtractTextContentRegex();
    [System.Text.RegularExpressions.GeneratedRegex("<script", System.Text.RegularExpressions.RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex ScriptCountRegex();
}
