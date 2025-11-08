# API Endpoint Implementation Plan: GET /api/v1/feed-analyses/{analysisId}

## 1. Endpoint Overview

Retrieves the complete analysis payload for a specific feed analysis, including AI-generated selectors, preflight check results, warnings, and approval status. This endpoint supports dashboard views and analysis detail pages where users review AI recommendations before approving a feed configuration.

## 2. Request Details

- **HTTP Method**: GET
- **URL Structure**: `/api/v1/feed-analyses/{analysisId}`
- **Parameters**:
  - **Path**: 
    - `analysisId` (uuid, required) - Unique identifier of the feed analysis
  - **Query**: None
  - **Body**: None
- **Authentication**: Required - JWT bearer token
- **Authorization**: User must own the feed analysis (resource ownership check on `UserId`)

## 3. Used Types

### Request Contracts

No request body for GET endpoint. Path parameter `analysisId` is bound from route.

### Response Contracts

**Location**: `RSSVibe.Contracts/FeedAnalyses/FeedAnalysisDetailResponse.cs`

```csharp
namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Detailed feed analysis response including selectors, preflight checks, and warnings.
/// </summary>
public sealed record FeedAnalysisDetailResponse(
    Guid AnalysisId,
    string TargetUrl,
    string NormalizedUrl,
    FeedAnalysisStatus Status,
    FeedPreflightChecks[] PreflightChecks,
    FeedPreflightDetails PreflightDetails,
    FeedSelectors? Selectors,
    string[] Warnings,
    string? AiModel,
    Guid? ApprovedFeedId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Dependent Types** (already exist in `RSSVibe.Contracts/FeedAnalyses/`):
- `FeedAnalysisStatus` - enum: pending, completed, failed, superseded
- `FeedPreflightChecks` - [Flags] enum: None, RequiresJavascript, RequiresAuthentication, Paywalled, InvalidMarkup, RateLimited, UnknownIssue
- `FeedSelectors` - record with list, item, title, link, published, summary properties
- `FeedPreflightDetails` - record with preflight metadata

### Service Layer Types

**Location**: `RSSVibe.Services/FeedAnalyses/`

```csharp
// GetFeedAnalysisCommand.cs
public sealed record GetFeedAnalysisCommand(
    Guid AnalysisId,
    Guid UserId  // From JWT claims for ownership verification
);

// GetFeedAnalysisResult.cs
public sealed record GetFeedAnalysisResult
{
    public bool Success { get; init; }
    public FeedAnalysis? Analysis { get; init; }
    public FeedAnalysisError? Error { get; init; }

    public static GetFeedAnalysisResult Succeeded(FeedAnalysis analysis) =>
        new() { Success = true, Analysis = analysis };

    public static GetFeedAnalysisResult Failed(FeedAnalysisError error) =>
        new() { Success = false, Error = error };
}

// FeedAnalysisError.cs (extend existing enum)
public enum FeedAnalysisError
{
    // ... existing errors ...
    NotFound,
    Unauthorized
}
```

**Service Interface Extension** (in `IFeedAnalysisService.cs`):

```csharp
public interface IFeedAnalysisService
{
    // ... existing methods ...
    
    Task<GetFeedAnalysisResult> GetFeedAnalysisAsync(
        GetFeedAnalysisCommand command, 
        CancellationToken ct);
}
```

### Validation Rules

No validation needed - simple ID lookup with ownership check. No FluentValidation validator required.

## 4. Response Details

### Success Response (200 OK)

```json
{
  "analysisId": "01936f8a-4567-7890-abcd-ef1234567890",
  "targetUrl": "https://example.com/news",
  "normalizedUrl": "https://example.com/news",
  "status": "completed",
  "preflightChecks": ["RequiresJavascript"],
  "preflightDetails": {
    "requiresAuthentication": false,
    "detectedPaywall": false
  },
  "selectors": {
    "list": ".article-list",
    "item": ".article",
    "title": ".article-title",
    "link": ".article-link",
    "published": ".article-date",
    "summary": ".article-summary"
  },
  "warnings": ["RequiresJavascript", "Possible rate limiting detected"],
  "aiModel": "openrouter/gpt-4.1-mini",
  "approvedFeedId": "01936f8b-1234-7890-abcd-ef1234567890",
  "createdAt": "2024-05-01T12:01:00Z",
  "updatedAt": "2024-05-01T12:03:00Z"
}
```

**TypedResults Signature**:

```csharp
private static async Task<Results<Ok<FeedAnalysisDetailResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound>>
    HandleAsync(
        Guid analysisId,
        ClaimsPrincipal user,
        IFeedAnalysisService feedAnalysisService,
        CancellationToken ct)
{
    // Implementation returns:
    // - TypedResults.Ok(response) for success
    // - TypedResults.Unauthorized() for missing/invalid JWT
    // - TypedResults.Forbid() for ownership violation
    // - TypedResults.NotFound() for non-existent analysis
}
```

## 5. Data Flow

1. **Request arrives** at `GET /api/v1/feed-analyses/{analysisId}`
2. **Authentication middleware** validates JWT bearer token, populates `ClaimsPrincipal`
3. **Route binding** extracts `analysisId` from path parameter
4. **Endpoint handler** extracts `userId` from JWT claims (`ClaimTypes.NameIdentifier` or `sub`)
5. **Service invocation**: Call `feedAnalysisService.GetFeedAnalysisAsync(new GetFeedAnalysisCommand(analysisId, userId), ct)`
6. **Database query** (in service):
   - Query `FeedAnalyses` table by `Id == analysisId` using `AsNoTracking()` for read-only performance
   - Check if analysis exists
   - Verify `FeedAnalysis.UserId == command.UserId` (ownership check)
   - Return analysis entity or error result
7. **Authorization check** (in endpoint):
   - If result indicates `NotFound` → return `TypedResults.NotFound()`
   - If result indicates `Unauthorized` → return `TypedResults.Forbid()`
8. **Response mapping**:
   - Map `FeedAnalysis` entity to `FeedAnalysisDetailResponse`
   - Convert `PreflightChecks` int to `FeedPreflightChecks[]` array (flags enum)
   - Strongly-typed JSON properties (`Selectors`, `PreflightDetails`) serialize automatically
   - PostgreSQL text array `Warnings` maps to `string[]`
9. **Return response** with `TypedResults.Ok(response)`

**Transaction Boundaries**: None required - single read-only query

## 6. Security Considerations

### Authentication
- **JWT bearer token required** - enforced by endpoint authentication attribute
- Extract user identity from `ClaimsPrincipal` using `ClaimTypes.NameIdentifier` or `sub` claim

### Authorization
- **Resource ownership check**: Verify `FeedAnalysis.UserId == currentUserId` before returning data
- Return `403 Forbidden` if ownership check fails (prevents unauthorized access)
- Return `404 Not Found` if analysis doesn't exist
- **Enumeration protection**: API spec shows both 403 and 404, using 403 for explicit ownership failures

### Input Validation
- `analysisId` validated as GUID by ASP.NET Core model binding
- No additional validation required for simple ID lookup

### Rate Limiting
- Apply general authenticated endpoint rate limit (e.g., 100 requests/minute per user)
- Not critical for read-only operations but helps prevent abuse

### Data Exposure
- All returned data is user-owned analysis information (safe to expose)
- Strongly-typed JSON properties (`FeedSelectors`, `FeedPreflightDetails`) ensure schema correctness
- No PII or sensitive credentials in response

### Additional Security
- HTTPS enforced for all API endpoints
- CORS configured to allow only trusted frontend origins

## 7. Error Handling

| Scenario | Status Code | Response | Error Handling |
|----------|-------------|----------|----------------|
| Analysis found and user owns it | 200 OK | `FeedAnalysisDetailResponse` | Success path |
| Missing or invalid JWT token | 401 Unauthorized | ProblemDetails | Middleware handles automatically |
| Analysis exists but user doesn't own it | 403 Forbidden | ProblemDetails | `TypedResults.Forbid()` |
| Analysis not found (non-existent ID) | 404 Not Found | ProblemDetails | `TypedResults.NotFound()` |
| Database connection timeout | 503 Service Unavailable | ProblemDetails | Global exception handler catches |
| Invalid GUID format in path | 400 Bad Request | ProblemDetails | Model binding validation |

**Error Response Example** (403 Forbidden):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this feed analysis.",
  "traceId": "00-abc123-def456-00"
}
```

**Logging Strategy**:
- **Info**: Successful retrieval with `analysisId`
- **Warning**: Unauthorized access attempts with `userId`, `analysisId`, attempted owner ID
- **Error**: Database exceptions with correlation ID and stack trace

## 8. Performance Considerations

### Database Query Optimization
- **AsNoTracking()**: Use for read-only query to avoid change tracking overhead
- **Primary key lookup**: Query by `FeedAnalysis.Id` (indexed by default) for O(1) performance
- **No joins required**: All data in single table (selectors and preflight details are JSONB columns)
- **Index usage**: Clustered index on primary key `Id` (UUIDv7)

### Caching Strategy
- **FusionCache** (optional for future optimization):
  - Cache key: `feed-analysis:{analysisId}`
  - TTL: 5 minutes (analysis rarely changes after completion)
  - Invalidate on PATCH operations (selector updates, rerun)
- **Not required for MVP** - database query is fast enough with primary key lookup

### Response Size
- Typical response size: ~2-5 KB (small JSON payload)
- No pagination needed (single resource)
- JSONB properties (`Selectors`, `PreflightDetails`) are compact

### Potential Bottlenecks
- Database connection pool exhaustion under high concurrency (mitigated by connection pooling)
- Large `Warnings` arrays (unlikely - typically 0-5 warnings)

### Optimization Opportunities
- Add ETag support for conditional requests (`If-None-Match` header)
- Compress responses with Brotli/Gzip for large `PreflightDetails` payloads
- Consider projection if only subset of fields needed in future

## 9. Implementation Steps

### Step 1: Create Contract Types
**File**: `src/RSSVibe.Contracts/FeedAnalyses/FeedAnalysisDetailResponse.cs`

```csharp
namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Detailed feed analysis response including selectors, preflight checks, and warnings.
/// </summary>
public sealed record FeedAnalysisDetailResponse(
    Guid AnalysisId,
    string TargetUrl,
    string NormalizedUrl,
    FeedAnalysisStatus Status,
    FeedPreflightChecks[] PreflightChecks,
    FeedPreflightDetails PreflightDetails,
    FeedSelectors? Selectors,
    string[] Warnings,
    string? AiModel,
    Guid? ApprovedFeedId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

### Step 2: Create Service Layer Types
**File**: `src/RSSVibe.Services/FeedAnalyses/GetFeedAnalysisCommand.cs`

```csharp
namespace RSSVibe.Services.FeedAnalyses;

public sealed record GetFeedAnalysisCommand(
    Guid AnalysisId,
    Guid UserId
);
```

**File**: `src/RSSVibe.Services/FeedAnalyses/GetFeedAnalysisResult.cs`

```csharp
namespace RSSVibe.Services.FeedAnalyses;

public sealed record GetFeedAnalysisResult
{
    public bool Success { get; init; }
    public FeedAnalysis? Analysis { get; init; }
    public FeedAnalysisError? Error { get; init; }

    public static GetFeedAnalysisResult Succeeded(FeedAnalysis analysis) =>
        new() { Success = true, Analysis = analysis };

    public static GetFeedAnalysisResult Failed(FeedAnalysisError error) =>
        new() { Success = false, Error = error };
}
```

**File**: `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisError.cs` (extend existing)

Add to existing enum:
```csharp
public enum FeedAnalysisError
{
    // ... existing errors ...
    NotFound,
    Unauthorized
}
```

### Step 3: Implement Service Method
**File**: `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisService.cs`

Add method to existing service:
```csharp
public async Task<GetFeedAnalysisResult> GetFeedAnalysisAsync(
    GetFeedAnalysisCommand command,
    CancellationToken ct)
{
    var analysis = await dbContext.FeedAnalyses
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.Id == command.AnalysisId, ct);

    if (analysis is null)
    {
        return GetFeedAnalysisResult.Failed(FeedAnalysisError.NotFound);
    }

    if (analysis.UserId != command.UserId)
    {
        logger.LogWarning(
            "Unauthorized access attempt to feed analysis {AnalysisId} by user {UserId}. Owner is {OwnerId}",
            command.AnalysisId, command.UserId, analysis.UserId);
        return GetFeedAnalysisResult.Failed(FeedAnalysisError.Unauthorized);
    }

    return GetFeedAnalysisResult.Succeeded(analysis);
}
```

**File**: `src/RSSVibe.Services/FeedAnalyses/IFeedAnalysisService.cs` (extend interface)

```csharp
public interface IFeedAnalysisService
{
    // ... existing methods ...
    
    Task<GetFeedAnalysisResult> GetFeedAnalysisAsync(
        GetFeedAnalysisCommand command,
        CancellationToken ct);
}
```

### Step 4: Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/GetFeedAnalysisEndpoint.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class GetFeedAnalysisEndpoint
{
    public static RouteGroupBuilder MapGetFeedAnalysisEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{analysisId:guid}", HandleAsync)
            .WithName("GetFeedAnalysis")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get feed analysis details";
                operation.Description = "Retrieves the complete analysis payload including selectors, preflight checks, and warnings.";
                return operation;
            })
            .RequireAuthorization()
            .Produces<FeedAnalysisDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<FeedAnalysisDetailResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound>>
        HandleAsync(
            Guid analysisId,
            ClaimsPrincipal user,
            IFeedAnalysisService feedAnalysisService,
            CancellationToken ct)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new GetFeedAnalysisCommand(analysisId, userId);
        var result = await feedAnalysisService.GetFeedAnalysisAsync(command, ct);

        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.NotFound => TypedResults.NotFound(),
                FeedAnalysisError.Unauthorized => TypedResults.Forbid(),
                _ => TypedResults.NotFound()
            };
        }

        var analysis = result.Analysis!;
        var response = new FeedAnalysisDetailResponse(
            AnalysisId: analysis.Id,
            TargetUrl: analysis.TargetUrl,
            NormalizedUrl: analysis.NormalizedUrl,
            Status: analysis.AnalysisStatus,
            PreflightChecks: ConvertPreflightChecks(analysis.PreflightChecks),
            PreflightDetails: analysis.PreflightDetails,
            Selectors: analysis.Selectors,
            Warnings: analysis.Warnings,
            AiModel: analysis.AiModel,
            ApprovedFeedId: analysis.ApprovedFeedId,
            CreatedAt: analysis.CreatedAt,
            UpdatedAt: analysis.UpdatedAt
        );

        return TypedResults.Ok(response);
    }

    private static FeedPreflightChecks[] ConvertPreflightChecks(int preflightChecks)
    {
        var checks = (FeedPreflightChecks)preflightChecks;
        return Enum.GetValues<FeedPreflightChecks>()
            .Where(c => c != FeedPreflightChecks.None && checks.HasFlag(c))
            .ToArray();
    }
}
```

### Step 5: Register Endpoint in Group
**File**: `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/FeedAnalysesGroup.cs`

Add to existing group:
```csharp
public static class FeedAnalysesGroup
{
    public static IEndpointRouteBuilder MapFeedAnalysesGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/feed-analyses")
            .WithTags("FeedAnalyses");

        // ... existing endpoint registrations ...
        group.MapGetFeedAnalysisEndpoint();

        return endpoints;
    }
}
```

### Step 6: Add Integration Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/FeedAnalyses/GetFeedAnalysisEndpointTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

public class GetFeedAnalysisEndpointTests : TestsBase
{
    [Test]
    public async Task GetFeedAnalysis_WithValidId_ShouldReturnAnalysisDetails()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        
        // Create test analysis in database
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var testUser = await userManager.FindByEmailAsync(TestApplication.TestUserEmail);
        
        var analysis = new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = testUser!.Id,
            TargetUrl = "https://example.com/news",
            NormalizedUrl = "https://example.com/news",
            AnalysisStatus = FeedAnalysisStatus.Completed,
            PreflightChecks = (int)FeedPreflightChecks.RequiresJavascript,
            PreflightDetails = new FeedPreflightDetails { /* ... */ },
            Selectors = new FeedSelectors { /* ... */ },
            Warnings = new[] { "RequiresJavascript" },
            AiModel = "openrouter/gpt-4.1-mini"
        };
        
        dbContext.FeedAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysis.Id}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        
        var responseData = await response.Content.ReadFromJsonAsync<FeedAnalysisDetailResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.AnalysisId).IsEqualTo(analysis.Id);
        await Assert.That(responseData.TargetUrl).IsEqualTo("https://example.com/news");
        await Assert.That(responseData.Status).IsEqualTo(FeedAnalysisStatus.Completed);
        await Assert.That(responseData.PreflightChecks).Contains(FeedPreflightChecks.RequiresJavascript);
    }

    [Test]
    public async Task GetFeedAnalysis_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var nonExistentId = Guid.CreateVersion7();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{nonExistentId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetFeedAnalysis_WithOtherUsersAnalysis_ShouldReturnForbidden()
    {
        // Arrange - Create analysis owned by different user
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        
        var otherUserId = Guid.CreateVersion7();
        var analysis = new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = otherUserId,
            TargetUrl = "https://example.com/news",
            NormalizedUrl = "https://example.com/news",
            // ... other required fields
        };
        
        dbContext.FeedAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysis.Id}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetFeedAnalysis_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var analysisId = Guid.CreateVersion7();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysisId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
```

### Step 7: Update OpenAPI Documentation

OpenAPI metadata already added in Step 4 via `.WithOpenApi()`. Verify Swagger UI shows:
- Endpoint: `GET /api/v1/feed-analyses/{analysisId}`
- Parameters: `analysisId` (path, uuid, required)
- Responses: 200 (OK), 401 (Unauthorized), 403 (Forbidden), 404 (Not Found)
- Security: Bearer token required

---

**Implementation Complete** - The endpoint retrieves feed analysis details with proper authentication, authorization, error handling, and performance optimization following RSSVibe architectural patterns.
