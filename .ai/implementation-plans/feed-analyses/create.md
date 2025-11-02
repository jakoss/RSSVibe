# API Endpoint Implementation Plan: POST /api/v1/feed-analyses

## 1. Endpoint Overview

The `POST /api/v1/feed-analyses` endpoint initiates an AI-powered analysis and preflight checks for a submitted URL. This operation performs **synchronous preflight checks** (basic HTTP validation, JavaScript detection, authentication requirements) and returns `202 Accepted` with the initial preflight results. The endpoint then enqueues a background workflow for deeper AI-driven selector detection and analysis.

**Key Characteristics:**
- **Synchronous preflight checks**: Runs basic validation immediately (HTTP accessibility, JavaScript detection, paywall detection)
- **Async AI analysis**: Deep selector analysis runs in background
- Requires authentication (JWT bearer token)
- Enforces unique constraint per user on normalized URL
- Supports force reanalysis with cooldown protection
- Returns 422 if preflight checks fail critically (unreachable URL, requires authentication)

## 2. Request Details

- **HTTP Method**: `POST`
- **URL Structure**: `/api/v1/feed-analyses`
- **Parameters**:
  - **Body** (JSON):
    ```json
    {
      "targetUrl": "https://example.com/news",
      "aiModel": "openrouter/gpt-4.1-mini",
      "forceReanalysis": false
    }
    ```
    - `targetUrl` (required): Absolute HTTP/HTTPS URL to analyze
    - `aiModel` (optional): Specific AI model identifier for OpenRouter
    - `forceReanalysis` (required boolean): If true, bypass duplicate check (subject to cooldown)

- **Authentication**: Required - JWT bearer token
- **Authorization**: User can only create analyses for themselves (UserId extracted from JWT claims)

## 3. Used Types

### Request Contracts

**File**: `src/RSSVibe.Contracts/FeedAnalyses/CreateFeedAnalysisRequest.cs` (✅ Already exists)

```csharp
public sealed record CreateFeedAnalysisRequest(
    string TargetUrl,
    string? AiModel,
    bool ForceReanalysis
)
{
    public sealed class Validator : AbstractValidator<CreateFeedAnalysisRequest>
    {
        public Validator()
        {
            RuleFor(x => x.TargetUrl)
                .NotEmpty().WithMessage("Target URL is required")
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var result) 
                    && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                .WithMessage("Target URL must be a valid absolute HTTP/HTTPS URL");

            RuleFor(x => x.AiModel)
                .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
                .WithMessage("AI model must be either null or a non-empty string");

            RuleFor(x => x.ForceReanalysis)
                .NotNull().WithMessage("ForceReanalysis must be specified");
        }
    }
}
```

**Additional Validation Needed in Service Layer:**
- URL normalization (canonicalized form)
- SSRF protection (reject internal/private IP ranges)
- **Synchronous preflight checks**: HTTP accessibility, JavaScript detection, paywall/authentication detection
- OpenRouter configuration availability check
- Cooldown enforcement for force reanalysis

### Response Contracts

**File**: `src/RSSVibe.Contracts/FeedAnalyses/CreateFeedAnalysisResponse.cs` (✅ Already exists)

```csharp
public sealed record CreateFeedAnalysisResponse(
    Guid AnalysisId,
    string Status,
    string NormalizedUrl,
    FeedPreflightDto Preflight,
    DateTimeOffset CreatedAt
);
```

**File**: `src/RSSVibe.Contracts/FeedAnalyses/FeedPreflightDto.cs` (✅ Already exists)

```csharp
public sealed record FeedPreflightDto(
    string[] Checks,
    Dictionary<string, object>? Details
);
```

### Service Layer Types

**File**: `src/RSSVibe.Services/FeedAnalyses/IFeedAnalysisService.cs` (❌ Need to create)

```csharp
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
```

**File**: `src/RSSVibe.Services/FeedAnalyses/IPreflightService.cs` (❌ Need to create)

```csharp
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
```

**File**: `src/RSSVibe.Services/FeedAnalyses/CreateFeedAnalysisCommand.cs` (❌ Need to create)

```csharp
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command to create a new feed analysis.
/// </summary>
public sealed record CreateFeedAnalysisCommand(
    Guid UserId,
    string TargetUrl,
    string? AiModel,
    bool ForceReanalysis
);
```

**File**: `src/RSSVibe.Services/FeedAnalyses/CreateFeedAnalysisResult.cs` (❌ Need to create)

```csharp
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of feed analysis creation operation.
/// </summary>
public sealed record CreateFeedAnalysisResult
{
    public bool Success { get; init; }
    public Guid AnalysisId { get; init; }
    public string? NormalizedUrl { get; init; }
    public FeedAnalysisStatus? Status { get; init; }
    public FeedPreflightChecks? PreflightChecks { get; init; }
    public FeedPreflightDetails? PreflightDetails { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public FeedAnalysisError? Error { get; init; }
    public string? ErrorDetail { get; init; }
}
```

**File**: `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisError.cs` (❌ Need to create)

```csharp
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
```

**File**: `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisService.cs` (❌ Need to create)

```csharp
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service implementation for feed analysis operations.
/// </summary>
internal sealed class FeedAnalysisService(
    RssVibeDbContext dbContext,
    ILogger<FeedAnalysisService> logger) : IFeedAnalysisService
{
    private const int ReanalysisCooldownMinutes = 15;

    public async Task<CreateFeedAnalysisResult> CreateFeedAnalysisAsync(
        CreateFeedAnalysisCommand command,
        CancellationToken cancellationToken = default)
    {
        // Implementation in step 9
    }

    private static string NormalizeUrl(string url)
    {
        // Normalize URL: lower-case host, trim paths, strip fragments
        var uri = new Uri(url);
        var normalized = $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
        if (!string.IsNullOrEmpty(uri.Query))
        {
            normalized += uri.Query;
        }
        return normalized;
    }

    private static bool IsInternalUrl(Uri uri)
    {
        // SSRF protection: check if URL points to internal network
        if (uri.IsLoopback) return true;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        
        // Check for private IP ranges (10.x, 172.16-31.x, 192.168.x)
        if (IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            var bytes = ipAddress.GetAddressBytes();
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
        }
        
        return false;
    }
}
```

**File**: `src/RSSVibe.Services/FeedAnalyses/PreflightService.cs` (❌ Need to create)

```csharp
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service for performing preflight checks on target URLs.
/// </summary>
internal sealed class PreflightService(
    IHttpClientFactory httpClientFactory,
    ILogger<PreflightService> logger) : IPreflightService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

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
            httpClient.Timeout = RequestTimeout;

            // Perform HTTP request
            var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.Add("User-Agent", "RSSVibe/1.0 (+https://rssvibe.com/bot)");

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            details["statusCode"] = ((int)response.StatusCode).ToString();
            details["contentType"] = response.Content.Headers.ContentType?.ToString() ?? "unknown";

            // Check status code
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
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

                details["contentLength"] = content.Length.ToString();
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
            AdditionalInfo = details
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
            var noscriptContent = htmlContent.Substring(
                htmlContent.IndexOf("<noscript>", StringComparison.OrdinalIgnoreCase));
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
            var bodyContent = htmlContent.Substring(bodyStart, bodyEnd - bodyStart);
            // If body has very little content but many scripts, likely SPA
            var scriptCount = System.Text.RegularExpressions.Regex.Matches(bodyContent, "<script", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            var textContent = System.Text.RegularExpressions.Regex.Replace(bodyContent, "<[^>]+>", "").Trim();
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
}
```

### Validation Rules

Validation handled by FluentValidation (automatic via SharpGrip.FluentValidation.AutoValidation.Endpoints):

1. **TargetUrl**: Not empty, absolute HTTP/HTTPS URL
2. **AiModel**: Either null or non-empty string
3. **ForceReanalysis**: Not null boolean

Additional business rule validations in service layer:
- URL normalization and SSRF protection
- Duplicate analysis check (unless ForceReanalysis=true)
- Cooldown enforcement for force reanalysis (15-minute window)
- OpenRouter configuration availability

## 4. Response Details

### Success Response (202 Accepted)

```json
{
  "analysisId": "01234567-89ab-cdef-0123-456789abcdef",
  "status": "Pending",
  "normalizedUrl": "https://example.com/news",
  "preflight": {
    "checks": ["RequiresJavascript"],
    "details": {
      "requiresAuthentication": false,
      "contentType": "text/html",
      "statusCode": 200
    }
  },
  "createdAt": "2025-11-02T10:30:00Z"
}
```

**Note**: The `preflight.checks` array contains the results of **synchronous preflight checks** performed during the request. These checks include:
- HTTP accessibility (status code 200-299)
- JavaScript detection (checking for SPA frameworks, dynamically loaded content)
- Authentication requirements (401/403 responses)
- Paywall detection (metered content, login walls)
- Invalid markup detection (parsing errors)

**Headers**:
- `Location: /api/v1/feed-analyses/{analysisId}`
- `Content-Type: application/json`

### Error Responses

| Status Code | Scenario | Response Body |
|-------------|----------|---------------|
| 400 Bad Request | Invalid URL format, missing OpenRouter configuration | `{ "title": "Invalid request", "detail": "...", "status": 400 }` |
| 401 Unauthorized | Missing or invalid JWT token | `{ "title": "Unauthorized", "status": 401 }` |
| 403 Forbidden | SSRF protection triggered (internal URL) | `{ "title": "Forbidden URL", "detail": "Target URL points to restricted network", "status": 403 }` |
| 409 Conflict | Duplicate normalized URL for user (without forceReanalysis) | `{ "title": "Analysis already exists", "detail": "An analysis for this URL already exists", "status": 409 }` |
| 422 Unprocessable Entity | Preflight validation failure (URL unreachable, requires authentication, critical error) | `{ "title": "Preflight validation failed", "detail": "Target URL requires authentication", "status": 422 }` |
| 429 Too Many Requests | Reanalysis cooldown active | `{ "title": "Too many requests", "detail": "Please wait 15 minutes between reanalysis attempts", "status": 429, "extensions": { "retryAfter": "2025-11-02T10:45:00Z" } }` |
| 503 Service Unavailable | AI provider unavailable, database error | `{ "title": "Service unavailable", "detail": "Unable to process analysis request", "status": 503 }` |

## 5. Data Flow

1. **Request Validation** (automatic via FluentValidation)
   - Validate request body schema
   - Check URL format and absoluteness
   - Validate AI model format if provided

2. **Authentication & Authorization**
   - Extract `UserId` from JWT claims (`sub` claim)
   - Verify user is authenticated

3. **URL Normalization & SSRF Protection**
   - Parse target URL
   - Apply SSRF protection (reject internal/private IPs, localhost)
   - If internal URL detected → Return 403 Forbidden
   - Normalize: lower-case host, trim paths, strip fragments
   - Store both original and normalized URLs

4. **Duplicate Detection**
   - Query `FeedAnalyses` table for existing analysis:
     - Filter: `UserId = currentUser.Id AND NormalizedUrl = normalizedTargetUrl`
   - If exists and `ForceReanalysis = false` → Return 409 Conflict
   - If exists and `ForceReanalysis = true`:
     - Check cooldown: if `CreatedAt` or last rerun < 15 minutes ago → Return 429
     - Mark existing analysis as `Superseded` status
     - Continue to create new analysis

5. **Synchronous Preflight Checks** ⭐ **NEW**
   - Call `IPreflightService.PerformPreflightChecksAsync(targetUrl)`
   - Preflight service performs:
     - **HTTP Accessibility Check**: Send HEAD/GET request with timeout (5-10 seconds)
       - Check status code (200-299 = OK, 401/403 = requires auth, 404/500 = error)
       - If unreachable or 5xx error → Set `UnknownIssue` flag
       - If 401/403 → Set `RequiresAuthentication` flag
     - **JavaScript Detection**: Check response for SPA frameworks, dynamic content loaders
       - Analyze HTML for `<script>` tags with React/Vue/Angular
       - Check for `<noscript>` warnings
       - If detected → Set `RequiresJavascript` flag
     - **Paywall Detection**: Look for common paywall indicators
       - Check for metered content warnings in HTML
       - Look for subscription/login prompts
       - If detected → Set `Paywalled` flag
     - **Invalid Markup**: Attempt HTML parsing
       - If parsing fails → Set `InvalidMarkup` flag
     - **Rate Limiting**: Check for 429 status or rate limit headers
       - If detected → Set `RateLimited` flag
   - Collect warnings (non-blocking issues like "Possible JavaScript required")
   - Determine if critical failure (unreachable, requires auth without workaround)
   - If critical failure → Return 422 Unprocessable Entity with details
   - If non-critical warnings → Continue with creation

6. **AI Configuration Check**
   - Validate OpenRouter configuration exists
   - If unavailable → Log warning but continue (AI analysis will fail in background)

7. **Create FeedAnalysis Entity**
   - Generate UUIDv7 using `Guid.CreateVersion7()`
   - Set fields:
     - `Id` = generated UUID
     - `UserId` = from JWT
     - `TargetUrl` = original URL
     - `NormalizedUrl` = normalized URL
     - `AnalysisStatus` = `FeedAnalysisStatus.Pending`
     - `PreflightChecks` = **from preflight result** (may have flags set)
     - `PreflightDetails` = **from preflight result** (includes status code, content type, etc.)
     - `Selectors` = null (will be populated by background job)
     - `Warnings` = **from preflight result** (array of warning messages)
     - `AiModel` = provided or null
     - `CreatedAt` = `DateTimeOffset.UtcNow`
   - Add to DbContext

8. **Persist to Database**
   - Call `SaveChangesAsync`
   - Handle unique constraint violation (race condition) → 409 Conflict
   - Handle database errors → 503

9. **Enqueue Background Job**
   - Enqueue AI analysis background job with `AnalysisId`
   - Background job will:
     - Fetch URL content (with JavaScript rendering if needed)
     - Run AI analysis for selector detection
     - Update FeedAnalysis with selector results
     - Set status to `Completed` or `Failed`
   - (Note: Background job implementation is out of scope for this endpoint)

10. **Return Response**
    - Map entity to `CreateFeedAnalysisResponse`
    - Convert `PreflightChecks` enum to string array for DTO
    - Include preflight details in response
    - Return `TypedResults.Accepted` with Location header

## 6. Security Considerations

### Authentication
- **Requirement**: JWT bearer token required in `Authorization: Bearer <token>` header
- **Implementation**: ASP.NET Core authentication middleware validates JWT
- **UserId extraction**: From JWT `sub` (subject) claim

### Authorization
- **Resource ownership**: Analysis tied to authenticated user via `UserId`
- **Scope**: User can only create analyses for themselves (no admin override)

### Input Validation
- **FluentValidation**: Automatic validation via SharpGrip integration
- **SSRF Protection**: 
  - Reject localhost, 127.0.0.1, ::1
  - Reject private IP ranges (10.x, 172.16-31.x, 192.168.x, 169.254.x)
  - Reject internal hostnames
- **URL Scheme**: Only allow HTTP/HTTPS (reject file://, ftp://, etc.)
- **URL Length**: Implicit PostgreSQL text column limit

### Rate Limiting
- **Endpoint-level**: Apply rate limiting policy to prevent abuse (e.g., 10 requests per minute per user)
- **Cooldown enforcement**: 15-minute cooldown for force reanalysis per URL
- **Configuration**: Use ASP.NET Core rate limiting middleware

### Data Security
- **URL logging**: Log normalized URLs only (no sensitive query params)
- **Error messages**: Don't expose internal system details in error responses
- **Correlation IDs**: Include for tracing without exposing sensitive data

### Additional Protections
- **HTTPS enforcement**: Require HTTPS in production
- **CORS**: Configure allowed origins for browser clients
- **SQL Injection**: Protected by Entity Framework parameterization
- **XSS**: Not applicable (JSON API, no HTML rendering)

## 7. Error Handling

| Scenario | Status Code | Response | Logging Level |
|----------|-------------|----------|---------------|
| Invalid URL format | 400 | `{ "title": "Validation failed", "errors": { "TargetUrl": ["..."] } }` | Warning |
| SSRF protection triggered | 403 | `{ "title": "Forbidden URL", "detail": "Target URL points to restricted network" }` | Warning |
| Missing JWT token | 401 | `{ "title": "Unauthorized" }` | Info |
| Invalid JWT token | 401 | `{ "title": "Unauthorized" }` | Warning |
| Duplicate analysis (no force) | 409 | `{ "title": "Analysis already exists", "detail": "..." }` | Info |
| Reanalysis cooldown active | 429 | `{ "title": "Too many requests", "detail": "Please wait 15 minutes...", "extensions": { "retryAfter": "..." } }` | Info |
| Preflight check: URL unreachable | 422 | `{ "title": "Preflight validation failed", "detail": "Failed to connect: ..." }` | Warning |
| Preflight check: Requires auth | 422 | `{ "title": "Preflight validation failed", "detail": "Target URL requires authentication" }` | Warning |
| Preflight check timeout | 422 | `{ "title": "Preflight validation failed", "detail": "Request timeout" }` | Warning |
| OpenRouter unavailable | 503 | `{ "title": "AI service unavailable", "detail": "Unable to connect to AI provider" }` | Error |
| Database connection error | 503 | `{ "title": "Service unavailable", "detail": "Database temporarily unavailable" }` | Error |
| Unique constraint violation | 409 | `{ "title": "Duplicate analysis", "detail": "Race condition detected" }` | Warning |
| Unexpected exception | 500 | `{ "title": "Internal server error", "detail": "An unexpected error occurred" }` | Critical |

**Logging Strategy**:
- Include correlation ID (`x-correlation-id` header)
- Log structured data: `UserId`, `TargetUrl` (normalized), `AnalysisId`
- Anonymize sensitive data in logs
- Use OpenTelemetry for distributed tracing

## 8. Performance Considerations

### Database Optimization
- **AsNoTracking**: Use `.AsNoTracking()` for duplicate detection query (read-only)
- **Indexes**: Leverage existing unique index on `(UserId, NormalizedUrl)`
- **Query optimization**: Single query for duplicate check
- **Execution strategy**: Use PostgreSQL execution strategy for transaction reliability

### Caching Strategy
- **Not applicable for POST**: Creation endpoints shouldn't cache
- **Future optimization**: Cache duplicate check results with short TTL (1-2 seconds) to handle burst requests

### Async Operations
- **Synchronous preflight checks**: Performed inline during request (timeout: 10 seconds max)
- **Background AI processing**: Deep selector analysis runs asynchronously (don't block beyond preflight)
- **Response time**: Endpoint returns after preflight checks + database save (~1-12 seconds depending on target URL)
- **Job queue**: Use TickerQ or similar for background AI job management

### Potential Bottlenecks
- **Preflight HTTP requests**: Each analysis performs HTTP request to target URL (1-10 seconds)
  - **Mitigation**: 10-second timeout, fail fast on unreachable URLs, consider caching recent preflight results
- **Database contention**: Unique constraint check under high concurrency
  - **Mitigation**: Use optimistic concurrency, handle unique violation gracefully
- **Concurrent preflight checks**: Many simultaneous requests to different URLs
  - **Mitigation**: Rate limiting at endpoint level, connection pooling in HttpClient
- **Background job queue**: Queue saturation if many analyses requested
  - **Mitigation**: Rate limiting, queue size monitoring, backpressure handling
- **AI provider latency**: OpenRouter API slowness
  - **Mitigation**: Separate concern (background job), timeout configuration

### Monitoring
- **Metrics**: Track endpoint latency (p50, p95, p99)
- **Alerts**: Alert on high error rates (>5% 503 responses)
- **Capacity planning**: Monitor analyses created per hour

## 9. Implementation Steps

### Step 1: Create Service Layer Types

**Create folder**: `src/RSSVibe.Services/FeedAnalyses/`

**Files to create**:
1. `IFeedAnalysisService.cs` - Service interface for analysis operations
2. `FeedAnalysisService.cs` - Service implementation
3. `IPreflightService.cs` - Service interface for preflight checks ⭐
4. `PreflightService.cs` - Preflight checks implementation ⭐
5. `CreateFeedAnalysisCommand.cs` - Command model
6. `CreateFeedAnalysisResult.cs` - Result model
7. `FeedAnalysisError.cs` - Error enum
8. `PreflightCheckResult.cs` - Preflight result record ⭐

**Implementation details**:
```csharp
// FeedAnalysisService.cs - Core implementation
internal sealed class FeedAnalysisService(
    RssVibeDbContext dbContext,
    ILogger<FeedAnalysisService> logger) : IFeedAnalysisService
{
    private const int ReanalysisCooldownMinutes = 15;

    public async Task<CreateFeedAnalysisResult> CreateFeedAnalysisAsync(
        CreateFeedAnalysisCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Normalize URL
            var normalizedUrl = NormalizeUrl(command.TargetUrl);
            
            // SSRF protection
            var targetUri = new Uri(command.TargetUrl);
            if (IsInternalUrl(targetUri))
            {
                logger.LogWarning("SSRF protection triggered for URL: {Url}", normalizedUrl);
                return new CreateFeedAnalysisResult
                {
                    Success = false,
                    Error = FeedAnalysisError.ForbiddenUrl,
                    ErrorDetail = "Target URL points to restricted network"
                };
            }

            // Check for duplicate analysis
            var existingAnalysis = await dbContext.FeedAnalyses
                .AsNoTracking()
                .Where(a => a.UserId == command.UserId && a.NormalizedUrl == normalizedUrl)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingAnalysis is not null)
            {
                if (!command.ForceReanalysis)
                {
                    logger.LogInformation(
                        "Duplicate analysis detected for user {UserId}, URL: {Url}",
                        command.UserId, normalizedUrl);
                    
                    return new CreateFeedAnalysisResult
                    {
                        Success = false,
                        Error = FeedAnalysisError.DuplicateAnalysis,
                        ErrorDetail = "An analysis for this URL already exists"
                    };
                }

                // Check cooldown period
                var cooldownEnd = existingAnalysis.CreatedAt.AddMinutes(ReanalysisCooldownMinutes);
                if (DateTimeOffset.UtcNow < cooldownEnd)
                {
                    logger.LogInformation(
                        "Reanalysis cooldown active for user {UserId}, URL: {Url}",
                        command.UserId, normalizedUrl);
                    
                    return new CreateFeedAnalysisResult
                    {
                        Success = false,
                        Error = FeedAnalysisError.ReanalysisCooldown,
                        ErrorDetail = $"Please wait until {cooldownEnd:u} before requesting reanalysis"
                    };
                }

                // Mark existing analysis as superseded
                var existingEntity = await dbContext.FeedAnalyses
                    .Where(a => a.Id == existingAnalysis.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (existingEntity is not null)
                {
                    existingEntity.AnalysisStatus = FeedAnalysisStatus.Superseded;
                }
            }

            // Perform synchronous preflight checks
            var preflightResult = await preflightService.PerformPreflightChecksAsync(
                command.TargetUrl, 
                cancellationToken);

            // If preflight checks indicate critical failure, reject the request
            if (preflightResult.IsCriticalFailure)
            {
                logger.LogWarning(
                    "Preflight checks failed for URL: {Url}, Checks: {Checks}",
                    normalizedUrl, preflightResult.Checks);
                
                return new CreateFeedAnalysisResult
                {
                    Success = false,
                    Error = FeedAnalysisError.PreflightFailed,
                    ErrorDetail = $"Preflight validation failed: {string.Join(", ", preflightResult.Warnings)}"
                };
            }

            // Create new analysis entity
            var analysis = new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = command.UserId,
                TargetUrl = command.TargetUrl,
                NormalizedUrl = normalizedUrl,
                AnalysisStatus = FeedAnalysisStatus.Pending,
                PreflightChecks = preflightResult.Checks,
                PreflightDetails = preflightResult.Details,
                Selectors = null,
                Warnings = preflightResult.Warnings,
                AiModel = command.AiModel,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.FeedAnalyses.Add(analysis);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Feed analysis created: {AnalysisId} for user {UserId}, URL: {Url}, PreflightChecks: {Checks}",
                analysis.Id, command.UserId, normalizedUrl, analysis.PreflightChecks);

            // TODO: Enqueue background job for AI analysis

            return new CreateFeedAnalysisResult
            {
                Success = true,
                AnalysisId = analysis.Id,
                NormalizedUrl = analysis.NormalizedUrl,
                Status = analysis.AnalysisStatus,
                PreflightChecks = analysis.PreflightChecks,
                PreflightDetails = analysis.PreflightDetails,
                CreatedAt = analysis.CreatedAt
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique constraint") == true)
        {
            logger.LogWarning(ex, "Unique constraint violation during analysis creation");
            return new CreateFeedAnalysisResult
            {
                Success = false,
                Error = FeedAnalysisError.DuplicateAnalysis,
                ErrorDetail = "Duplicate analysis detected"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating feed analysis for user {UserId}", command.UserId);
            return new CreateFeedAnalysisResult
            {
                Success = false,
                Error = FeedAnalysisError.DatabaseError,
                ErrorDetail = "An error occurred while creating the analysis"
            };
        }
    }

    private static string NormalizeUrl(string url)
    {
        var uri = new Uri(url);
        var normalized = $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
        if (!string.IsNullOrEmpty(uri.Query))
        {
            normalized += uri.Query;
        }
        return normalized;
    }

    private static bool IsInternalUrl(Uri uri)
    {
        if (uri.IsLoopback) return true;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        
        if (IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            var bytes = ipAddress.GetAddressBytes();
            if (bytes.Length == 4) // IPv4
            {
                if (bytes[0] == 10) return true; // 10.0.0.0/8
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true; // 172.16.0.0/12
                if (bytes[0] == 192 && bytes[1] == 168) return true; // 192.168.0.0/16
                if (bytes[0] == 169 && bytes[1] == 254) return true; // 169.254.0.0/16 (link-local)
            }
        }
        
        return false;
    }
}
```

### Step 2: Register Services in Dependency Injection

**File**: `src/RSSVibe.Services/Extensions/ServiceCollectionExtensions.cs`

**Add registrations**:
```csharp
public static IServiceCollection AddRssVibeServices(this IServiceCollection services)
{
    // Existing auth service
    services.AddScoped<IAuthService, AuthService>();
    
    // Add feed analysis services
    services.AddScoped<IFeedAnalysisService, FeedAnalysisService>();
    services.AddScoped<IPreflightService, PreflightService>(); // ⭐ NEW
    
    return services;
}
```

**Configure HttpClient for PreflightService** (in `Program.cs` or service registration):
```csharp
// In Program.cs after services registration
builder.Services.AddHttpClient("PreflightClient")
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });
```

### Step 3: Create Endpoint File

**Create folder**: `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/`

**File**: `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/CreateFeedAnalysisEndpoint.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

/// <summary>
/// Endpoint for creating feed analysis requests.
/// </summary>
public static class CreateFeedAnalysisEndpoint
{
    /// <summary>
    /// Maps the POST /feed-analyses endpoint to the handler.
    /// </summary>
    public static RouteGroupBuilder MapCreateFeedAnalysisEndpoint(
        this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateFeedAnalysis")
            .RequireAuthorization()
            .WithOpenApi(operation =>
            {
                operation.Summary = "Create a new feed analysis";
                operation.Description = "Initiates AI-powered analysis and preflight checks for a submitted URL. " +
                                       "Returns immediately with 202 Accepted and provides location header for polling.";
                return operation;
            });

        return group;
    }

    /// <summary>
    /// Handles the feed analysis creation request.
    /// </summary>
    private static async Task<Results<
        Accepted<CreateFeedAnalysisResponse>,
        ValidationProblem,
        ProblemHttpResult,
        UnauthorizedHttpResult>>
        HandleAsync(
            CreateFeedAnalysisRequest request,
            ClaimsPrincipal user,
            IFeedAnalysisService feedAnalysisService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CreateFeedAnalysisEndpoint");

        // Extract UserId from JWT claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT claims");
            return TypedResults.Unauthorized();
        }

        // Map request to service command
        var command = new CreateFeedAnalysisCommand(
            userId,
            request.TargetUrl,
            request.AiModel,
            request.ForceReanalysis);

        // Invoke service layer
        var result = await feedAnalysisService.CreateFeedAnalysisAsync(command, cancellationToken);

        // Handle service result
        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.DuplicateAnalysis =>
                    TypedResults.Problem(
                        title: "Analysis already exists",
                        detail: "An analysis for this URL already exists. Use forceReanalysis to create a new one.",
                        statusCode: StatusCodes.Status409Conflict),

                FeedAnalysisError.ReanalysisCooldown =>
                    TypedResults.Problem(
                        title: "Too many requests",
                        detail: result.ErrorDetail ?? "Please wait 15 minutes between reanalysis attempts.",
                        statusCode: StatusCodes.Status429TooManyRequests),

                FeedAnalysisError.ForbiddenUrl =>
                    TypedResults.Problem(
                        title: "Forbidden URL",
                        detail: "Target URL points to a restricted network resource.",
                        statusCode: StatusCodes.Status403Forbidden),

                FeedAnalysisError.AiServiceUnavailable =>
                    TypedResults.Problem(
                        title: "AI service unavailable",
                        detail: "Unable to connect to AI provider. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                FeedAnalysisError.DatabaseError =>
                    TypedResults.Problem(
                        title: "Service unavailable",
                        detail: "Database temporarily unavailable. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                FeedAnalysisError.InvalidUrl =>
                    TypedResults.Problem(
                        title: "Invalid URL",
                        detail: result.ErrorDetail ?? "The provided URL could not be processed.",
                        statusCode: StatusCodes.Status400BadRequest),

                _ =>
                    TypedResults.Problem(
                        title: "Analysis creation failed",
                        detail: "An unexpected error occurred.",
                        statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // Map preflight checks enum to string array for DTO
        var preflightChecks = ConvertPreflightChecksToStringArray(result.PreflightChecks!.Value);
        
        // Construct success response
        var response = new CreateFeedAnalysisResponse(
            result.AnalysisId,
            result.Status!.Value.ToString(),
            result.NormalizedUrl!,
            new FeedPreflightDto(preflightChecks, null),
            result.CreatedAt!.Value);

        logger.LogInformation(
            "Feed analysis created: {AnalysisId} for user {UserId}",
            result.AnalysisId, userId);

        // Return 202 Accepted with Location header
        var location = $"/api/v1/feed-analyses/{result.AnalysisId}";
        return TypedResults.Accepted(location, response);
    }

    /// <summary>
    /// Converts FeedPreflightChecks flags enum to string array for API response.
    /// </summary>
    private static string[] ConvertPreflightChecksToStringArray(FeedPreflightChecks checks)
    {
        if (checks == FeedPreflightChecks.None)
        {
            return [];
        }

        var result = new List<string>();
        
        if (checks.HasFlag(FeedPreflightChecks.RequiresJavascript))
            result.Add("RequiresJavascript");
        if (checks.HasFlag(FeedPreflightChecks.RequiresAuthentication))
            result.Add("RequiresAuthentication");
        if (checks.HasFlag(FeedPreflightChecks.Paywalled))
            result.Add("Paywalled");
        if (checks.HasFlag(FeedPreflightChecks.InvalidMarkup))
            result.Add("InvalidMarkup");
        if (checks.HasFlag(FeedPreflightChecks.RateLimited))
            result.Add("RateLimited");
        if (checks.HasFlag(FeedPreflightChecks.UnknownIssue))
            result.Add("UnknownIssue");
        
        return [.. result];
    }
}
```

### Step 4: Create Feature Group

**File**: `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/FeedAnalysesGroup.cs`

```csharp
namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

/// <summary>
/// Hierarchical group for feed analysis endpoints.
/// </summary>
public static class FeedAnalysesGroup
{
    public static IEndpointRouteBuilder MapFeedAnalysesGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/feed-analyses")
            .WithTags("Feed Analyses");

        // Register all feed analysis endpoints
        group.MapCreateFeedAnalysisEndpoint();
        // TODO: Add other endpoints (GET list, GET detail, PATCH, etc.)

        return endpoints;
    }
}
```

### Step 5: Register Group in ApiGroup

**File**: `src/RSSVibe.ApiService/Endpoints/ApiGroup.cs`

**Update**:
```csharp
public static class ApiGroup
{
    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        // Register all feature groups
        group.MapAuthGroup();
        group.MapFeedAnalysesGroup(); // Add this line

        return endpoints;
    }
}
```

### Step 6: Create Custom Factory for Mocked Preflight Tests

**Important**: Following integration testing guidelines, use `WithWebHostBuilder` to create custom factories for different test scenarios instead of modifying `TestApplication` directly.

**Create helper method in test class** for creating factories with mocked preflight service:

```csharp
namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Integration tests for CreateFeedAnalysisEndpoint (POST /api/v1/feed-analyses).
/// Tests use WebApplicationFactory with mocked IPreflightService.
/// </summary>
public class CreateFeedAnalysisEndpointTests : TestsBase
{
    /// <summary>
    /// Creates a WebApplicationFactory with mocked IPreflightService that returns successful checks.
    /// This avoids real HTTP calls to dummy URLs in tests.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWithMockedPreflight(
        PreflightCheckResult? mockResult = null)
    {
        return WebApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove real preflight service registration
                services.RemoveAll<IPreflightService>();

                // Add mock preflight service
                var mock = Substitute.For<IPreflightService>();
                
                // Use provided result or default successful result
                var result = mockResult ?? new PreflightCheckResult(
                    FeedPreflightChecks.None,
                    new FeedPreflightDetails
                    {
                        RequiresJavascript = false,
                        RequiresAuthentication = false,
                        IsPaywalled = false,
                        HasInvalidMarkup = false,
                        IsRateLimited = false,
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "statusCode", "200" },
                            { "contentType", "text/html" }
                        }
                    },
                    [],
                    false
                );
                
                mock.PerformPreflightChecksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(result);
                
                services.AddSingleton(mock);
            });
        });
    }

    // Tests use this helper method...
}
```

**Benefits of this approach**:
- ✅ Follows integration testing guidelines (use `WithWebHostBuilder`)
- ✅ `TestApplication` remains clean (no test-specific mocking)
- ✅ Each test class controls its own mocking strategy
- ✅ Can customize mock behavior per test scenario
- ✅ Can test different preflight results easily

### Step 7: Add Integration Tests

**Create folder**: `Tests/RSSVibe.ApiService.Tests/Endpoints/FeedAnalyses/`

**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/FeedAnalyses/CreateFeedAnalysisEndpointTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;
using RSSVibe.Services.FeedAnalyses;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Integration tests for CreateFeedAnalysisEndpoint (POST /api/v1/feed-analyses).
/// Uses custom factory with mocked IPreflightService to avoid real HTTP calls.
/// </summary>
public class CreateFeedAnalysisEndpointTests : TestsBase
{
    /// <summary>
    /// Creates a custom factory with mocked IPreflightService.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWithMockedPreflight(
        PreflightCheckResult? mockResult = null)
    {
        return WebApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove real preflight service
                services.RemoveAll<IPreflightService>();

                // Add mock with customizable result
                var mock = Substitute.For<IPreflightService>();
                var result = mockResult ?? new PreflightCheckResult(
                    FeedPreflightChecks.None,
                    new FeedPreflightDetails
                    {
                        RequiresJavascript = false,
                        RequiresAuthentication = false,
                        IsPaywalled = false,
                        HasInvalidMarkup = false,
                        IsRateLimited = false,
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "statusCode", "200" },
                            { "contentType", "text/html" }
                        }
                    },
                    [],
                    false
                );

                mock.PerformPreflightChecksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(result);

                services.AddSingleton(mock);
            });
        });
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithValidRequest_ShouldReturnAcceptedWithAnalysisData()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/blog",
            AiModel: "openrouter/gpt-4.1-mini",
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Accepted);

        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.AnalysisId).IsNotEqualTo(Guid.Empty);
        await Assert.That(responseData.Status).IsEqualTo("Pending");
        await Assert.That(responseData.NormalizedUrl).IsEqualTo("https://example.com/blog");
        await Assert.That(responseData.Preflight).IsNotNull();
        await Assert.That(responseData.Preflight.Checks).IsEmpty();

        // Verify Location header
        await Assert.That(response.Headers.Location?.ToString())
            .IsEqualTo($"/api/v1/feed-analyses/{responseData.AnalysisId}");
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithDuplicateUrl_ShouldReturnConflict()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/duplicate",
            AiModel: null,
            ForceReanalysis: false
        );

        // Create first analysis
        await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Act - Try to create duplicate
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithForceReanalysis_ShouldCreateNewAnalysis()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var url = $"https://example.com/reanalysis-{Guid.CreateVersion7():N}";
        var firstRequest = new CreateFeedAnalysisRequest(
            TargetUrl: url,
            AiModel: null,
            ForceReanalysis: false
        );

        // Create first analysis
        var firstResponse = await client.PostAsJsonAsync("/api/v1/feed-analyses", firstRequest);
        var firstData = await firstResponse.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();

        // Wait for cooldown (in real test, would use time provider)
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act - Force reanalysis
        var reanalysisRequest = new CreateFeedAnalysisRequest(
            TargetUrl: url,
            AiModel: null,
            ForceReanalysis: true
        );
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", reanalysisRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Accepted);
        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();
        await Assert.That(responseData.AnalysisId).IsNotEqualTo(firstData.AnalysisId);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithInvalidUrl_ShouldReturnValidationError()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "not-a-valid-url",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithInternalUrl_ShouldReturnForbidden()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "http://localhost:8080/admin",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = factory.CreateClient(); // Unauthenticated
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/blog",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_VerifyDatabaseStateAfterCreation()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/db-test",
            AiModel: "test-model",
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);
        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();

        // Assert - Check database state
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var analysis = await dbContext.FeedAnalyses
            .FirstOrDefaultAsync(a => a.Id == responseData.AnalysisId);

        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.TargetUrl).IsEqualTo("https://example.com/db-test");
        await Assert.That(analysis.NormalizedUrl).IsEqualTo("https://example.com/db-test");
        await Assert.That(analysis.AnalysisStatus).IsEqualTo(FeedAnalysisStatus.Pending);
        await Assert.That(analysis.AiModel).IsEqualTo("test-model");
        await Assert.That(analysis.PreflightChecks).IsEqualTo(FeedPreflightChecks.None); // From mock
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithPreflightChecks_ShouldReturnChecksInResponse()
    {
        // Arrange - Use custom factory with mocked preflight
        var factory = CreateFactoryWithMockedPreflight();
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/blog",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);
        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();

        // Assert - Response includes preflight check results (mocked to return no issues)
        await Assert.That(responseData.Preflight).IsNotNull();
        await Assert.That(responseData.Preflight.Checks).IsNotNull();
        await Assert.That(responseData.Preflight.Checks).IsEmpty(); // Mock returns FeedPreflightChecks.None
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithJavaScriptDetection_ShouldStorePreflightFlag()
    {
        // Arrange - Create factory with mock that returns JavaScript detection
        var mockResult = new PreflightCheckResult(
            FeedPreflightChecks.RequiresJavascript,
            new FeedPreflightDetails
            {
                RequiresJavascript = true,
                RequiresAuthentication = false,
                IsPaywalled = false,
                HasInvalidMarkup = false,
                IsRateLimited = false,
                AdditionalInfo = new Dictionary<string, string>
                {
                    { "statusCode", "200" },
                    { "contentType", "text/html" }
                }
            },
            ["Target URL may require JavaScript rendering"],
            false // Not a critical failure
        );
        
        var factory = CreateFactoryWithMockedPreflight(mockResult);
        var client = CreateAuthenticatedClient(factory);
        
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/spa-app",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);
        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();

        // Assert - Verify JavaScript flag in response and database
        await Assert.That(responseData.Preflight.Checks).Contains("RequiresJavascript");
        
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var analysis = await dbContext.FeedAnalyses
            .FirstOrDefaultAsync(a => a.Id == responseData.AnalysisId);

        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.PreflightChecks).IsEqualTo(FeedPreflightChecks.RequiresJavascript);
        await Assert.That(analysis.Warnings).Contains("Target URL may require JavaScript rendering");
    }
}
```

### Step 8: Update TestsBase to Support Custom Factories

**File**: `Tests/RSSVibe.ApiService.Tests/TestsBase.cs`

**Update the `CreateAuthenticatedClient` method** to accept an optional factory parameter:

```csharp
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using RSSVibe.ApiService.Tests.Infrastructure;

namespace RSSVibe.ApiService.Tests;

public abstract class TestsBase
{
    [ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
    public required TestApplication WebApplicationFactory { get; init; }

    /// <summary>
    /// Creates an HTTP client authenticated with the test user's bearer token.
    /// </summary>
    /// <param name="factory">Optional custom factory. If null, uses the shared TestApplication factory.</param>
    /// <param name="customToken">Optional custom JWT token. If null, uses the test user's token from shared factory.</param>
    /// <returns>An authenticated HttpClient instance.</returns>
    protected HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program>? factory = null, 
        string? customToken = null)
    {
        var factoryToUse = factory ?? WebApplicationFactory;
        var client = factoryToUse.CreateClient();
        
        // Use custom token or get from base factory (shared test user)
        var token = customToken ?? WebApplicationFactory.TestUserBearerToken;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        
        return client;
    }
}
```

**Key changes**:
- ✅ Added optional `factory` parameter (defaults to `null`)
- ✅ If `factory` is `null`, uses shared `WebApplicationFactory`
- ✅ If `factory` is provided, uses the custom factory
- ✅ Maintains backward compatibility with existing tests

**Why this is needed**:
- Allows tests to pass custom factories created with `WithWebHostBuilder`
- Enables mocking of services (like `IPreflightService`) per test class
- Keeps test infrastructure flexible and extensible

**Usage examples**:
```csharp
// Example 1: Use default shared factory (existing tests - backward compatible)
var client = CreateAuthenticatedClient();

// Example 2: Use custom factory with mocked services
var factory = CreateFactoryWithMockedPreflight();
var client = CreateAuthenticatedClient(factory);

// Example 3: Use custom factory with custom token
var factory = CreateFactoryWithMockedPreflight();
var customToken = "custom-jwt-token";
var client = CreateAuthenticatedClient(factory, customToken);
```

### Step 9: Add NSubstitute Package (if not already present)

Integration tests require NSubstitute for mocking `IPreflightService`.

```bash
# Navigate to test project
cd Tests/RSSVibe.ApiService.Tests

# Add NSubstitute package
dotnet add package NSubstitute

# Restore packages
dotnet restore
```

**Note on Testing Strategy**:
- **Endpoint integration tests**: Mock `IPreflightService` using custom factories via `WithWebHostBuilder`
- **Different scenarios**: Create custom factories with different mock behaviors (success, JavaScript detected, critical failure)
- **Unit tests for PreflightService**: Create separate unit tests for `PreflightService` that test actual HTTP behavior
  - Test with real URLs (e.g., `https://example.com`)
  - Test JavaScript detection logic
  - Test paywall detection logic
  - Test timeout handling
  - Test error handling

**Optional**: Create `Tests/RSSVibe.Services.Tests/FeedAnalyses/PreflightServiceTests.cs` for unit testing the preflight logic in isolation.

### Step 10: Update OpenAPI Documentation

OpenAPI metadata is already configured via `.WithOpenApi()` in the endpoint registration.

**Additional considerations**:
- Ensure Swagger UI displays endpoint in "Feed Analyses" tag
- Document all response status codes
- Include example request/response in OpenAPI spec

### Step 11: Run Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~CreateFeedAnalysisEndpointTests"
```

### Step 12: Build and Verify

```bash
# Build with warnings as errors
dotnet build -c Release -p:TreatWarningsAsErrors=true

# Apply code formatting
dotnet format

# Verify formatting
dotnet format --verify-no-changes
```

### Step 13: Manual Testing Checklist

Use REST client (e.g., Postman, curl, HTTPie) to verify with **real URLs** (not mocked):

**Basic Functionality:**
- ✅ Successful analysis creation returns 202 with Location header
- ✅ Duplicate URL returns 409 without forceReanalysis
- ✅ ForceReanalysis creates new analysis after cooldown
- ✅ Invalid URL returns 400 with validation errors
- ✅ Internal URL (localhost, 10.x.x.x) returns 403
- ✅ Missing auth token returns 401

**Preflight Checks (Real HTTP Behavior):**
- ✅ Real website (e.g., `https://news.ycombinator.com`) → Successful with preflight results
- ✅ JavaScript-heavy site (e.g., `https://twitter.com`) → Detects `RequiresJavascript` flag
- ✅ Paywalled site (e.g., `https://www.nytimes.com/section/world`) → Detects `Paywalled` flag
- ✅ Auth-required URL → Returns 422 with `RequiresAuthentication`
- ✅ Non-existent domain → Returns 422 with connection failure
- ✅ Timeout-prone URL → Returns 422 after timeout

**Database & Integration:**
- ✅ Database state matches response data including preflight results
- ✅ Analysis appears in database with correct UserId and preflight details
- ✅ Warnings array populated for non-critical issues
- ✅ Swagger UI displays endpoint correctly

**Example curl command**:
```bash
curl -X POST https://localhost:7001/api/v1/feed-analyses \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "targetUrl": "https://example.com/blog",
    "aiModel": "openrouter/gpt-4.1-mini",
    "forceReanalysis": false
  }'
```

---

## Summary

This implementation plan provides comprehensive guidance for implementing the `POST /api/v1/feed-analyses` endpoint following RSSVibe's architecture patterns:

- **Service layer separation**: Business logic in `FeedAnalysisService` with dedicated `PreflightService`
- **Synchronous preflight checks**: Immediate validation of target URLs (HTTP accessibility, JavaScript/paywall detection)
- **Command/Result pattern**: Clear data flow with `CreateFeedAnalysisCommand` and `CreateFeedAnalysisResult`
- **TypedResults**: Type-safe endpoint responses
- **FluentValidation**: Automatic validation integration
- **Security**: SSRF protection, authentication, rate limiting
- **Error handling**: Comprehensive error scenarios with appropriate status codes (including 422 for preflight failures)
- **Testing strategy**: 
  - Integration tests with mocked `IPreflightService` (avoids real HTTP calls to dummy URLs)
  - Optional unit tests for `PreflightService` logic in isolation
- **Performance**: Database optimization with indexes and AsNoTracking, HTTP client pooling
- **Async operation**: Background job pattern for deep AI selector analysis

**Key Architectural Decisions**:
1. **Preflight checks run synchronously** (1-10 seconds) to provide immediate feedback
2. **Mock preflight service in tests** to avoid network dependencies and enable fast test execution
3. **Critical preflight failures** (unreachable, requires auth) return 422 immediately
4. **Non-critical warnings** (JavaScript, possible paywall) stored but allow analysis to proceed

The endpoint follows ASP.NET Core Minimal APIs best practices, Entity Framework Core patterns, and the hierarchical endpoint organization specified in project guidelines.
