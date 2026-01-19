# API Endpoint Implementation Plan: DELETE /api/v1/feed-analyses/{analysisId}

## 1. Endpoint Overview

This endpoint allows authenticated users to delete (cancel) their pending or in-progress feed analyses. Deletion is restricted to analyses in `pending` or `inProgress` status to preserve historical records. The operation verifies user ownership and, when background job integration is complete, will cancel any queued or running AI processing tasks.

**Business Rules:**
- Only analyses in `pending` or `inProgress` status can be deleted
- User must own the analysis (ownership verification via `UserId` match)
- Analyses in `completed`, `failed`, or `superseded` status are preserved as historical records and cannot be deleted (returns 422)
- Database cascading behavior: Deleting a `FeedAnalysis` will automatically set `Feeds.AnalysisId` to `null` due to "on delete set null" constraint
- Future: When background job system is implemented, deletion should cancel queued/running AI processing jobs

## 2. Request Details

- **HTTP Method:** DELETE
- **URL Structure:** `/api/v1/feed-analyses/{analysisId}`
- **Parameters:**
  - **Path:** `analysisId` (UUID, required) - The unique identifier of the feed analysis to delete
- **Authentication:** Required (JWT bearer token)
- **Authorization:** User must be the owner of the analysis (UserId match)

## 3. Used Types

### Request Contracts

No request body is needed for this endpoint (DELETE operation).

### Response Contracts

No response body (204 No Content on success).

### Service Layer Types

**Command:**
```csharp
// File: src/RSSVibe.Services/FeedAnalyses/DeleteFeedAnalysisCommand.cs
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Command for deleting (cancelling) a feed analysis.
/// Includes user context for ownership verification.
/// </summary>
public sealed record DeleteFeedAnalysisCommand(
    Guid AnalysisId,
    Guid UserId
);
```

**Result:**
```csharp
// File: src/RSSVibe.Services/FeedAnalyses/DeleteFeedAnalysisResult.cs
namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Result of a feed analysis deletion operation.
/// </summary>
public sealed record DeleteFeedAnalysisResult
{
    public bool Success { get; init; }
    public FeedAnalysisError? Error { get; init; }
    public string? ErrorDetail { get; init; }

    public static DeleteFeedAnalysisResult Succeeded() =>
        new() { Success = true };

    public static DeleteFeedAnalysisResult Failed(FeedAnalysisError error, string? detail = null) =>
        new() { Success = false, Error = error, ErrorDetail = detail };
}
```

**Service Interface Update:**
```csharp
// File: src/RSSVibe.Services/FeedAnalyses/IFeedAnalysisService.cs
// Add this method to the interface:

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
```

**Error Enum Update:**
```csharp
// File: src/RSSVibe.Services/FeedAnalyses/FeedAnalysisError.cs
// Add this new error value to the existing enum:

/// <summary>
/// Cannot delete analysis because it is not in a cancellable state (must be pending or inProgress).
/// </summary>
CannotCancelCompletedAnalysis
```

### API Client Interface

**Interface Update:**
```csharp
// File: src/RSSVibe.Contracts/IFeedAnalysesClient.cs
// Add this method to the interface:

/// <summary>
/// DELETE /api/v1/feed-analyses/{analysisId} - Delete a feed analysis.
/// </summary>
Task<ApiResultNoData> DeleteAsync(
    Guid analysisId,
    CancellationToken cancellationToken = default);
```

**Implementation:**
```csharp
// File: src/RSSVibe.Contracts/Internal/FeedAnalysesClient.cs
// Add this method to the FeedAnalysesClient class:

public async Task<ApiResultNoData> DeleteAsync(
    Guid analysisId,
    CancellationToken cancellationToken = default)
{
    var response = await httpClient.DeleteAsync(
        $"{BaseRoute}/{analysisId}",
        cancellationToken);

    return await HttpHelper.HandleResponseNoDataAsync(response, cancellationToken);
}
```

### Validation Rules

No FluentValidation validator is needed for this endpoint (only a path parameter).

Path parameter validation:
- `analysisId` must be a valid GUID (handled by routing)
- User must be authenticated (handled by `RequireAuthorization()`)

Business logic validation (in service layer):
- Analysis must exist (404 if not found)
- User must own the analysis (403 if ownership check fails)
- Analysis must be in `pending` or `inProgress` status (422 if not)

## 4. Response Details

**Success Response:**
- **Status Code:** 204 No Content
- **Body:** Empty

**Error Responses:**

| Status Code | Scenario | Response Body |
|-------------|----------|---------------|
| 401 | User not authenticated | Challenge response (no body) |
| 403 | User doesn't own the analysis | ProblemDetails with title "Forbidden" |
| 404 | Analysis not found | ProblemDetails with title "Analysis not found" |
| 422 | Analysis cannot be cancelled (not pending/inProgress) | ProblemDetails with title "Cannot cancel completed analysis" and detail explaining constraint |

All error responses use `TypedResults.Problem()` with RFC 7807 ProblemDetails format.

## 5. Data Flow

1. **Request Reception:**
   - HTTP DELETE request arrives with `analysisId` in path
   - ASP.NET Core routing extracts `analysisId` as `Guid`

2. **Authentication & Authorization:**
   - Authorization middleware validates JWT bearer token
   - Endpoint handler extracts `userId` from `ClaimsPrincipal` (`ClaimTypes.NameIdentifier`)

3. **Service Layer Invocation:**
   - Create `DeleteFeedAnalysisCommand(analysisId, userId)`
   - Call `IFeedAnalysisService.DeleteFeedAnalysisAsync(command, ct)`

4. **Service Layer Processing:**
   - Query database for `FeedAnalysis` by `Id` (single row lookup by primary key)
   - **Check 1:** If analysis not found → return `Failed(NotFound)`
   - **Check 2:** If `analysis.UserId != command.UserId` → return `Failed(Unauthorized)` and log warning
   - **Check 3:** If `analysis.AnalysisStatus` is `Completed`, `Failed`, or `Superseded` → return `Failed(CannotCancelCompletedAnalysis)` with detail message
   - If all checks pass:
     - Remove entity from DbContext using `dbContext.FeedAnalyses.Remove(analysis)`
     - Call `await dbContext.SaveChangesAsync(cancellationToken)`
     - TODO: When background job integration is complete, cancel any queued/running AI processing job
     - Log successful deletion with `AnalysisId` and `UserId`
     - Return `Succeeded()`

5. **Endpoint Response:**
   - If `result.Success == true` → return `TypedResults.NoContent()` (204)
   - If `result.Error == NotFound` → return `TypedResults.Problem(statusCode: 404)`
   - If `result.Error == Unauthorized` → return `TypedResults.Problem(statusCode: 403)`
   - If `result.Error == CannotCancelCompletedAnalysis` → return `TypedResults.Problem(statusCode: 422)`
   - If `result.Error == DatabaseError` → return `TypedResults.Problem(statusCode: 503)`

6. **Database Cascading:**
   - PostgreSQL automatically sets `Feeds.AnalysisId` to `NULL` for any feeds referencing this analysis (due to "on delete set null" FK constraint)
   - No manual cascade handling required in application code

## 6. Security Considerations

### Authentication
- **Required:** JWT bearer token in `Authorization` header
- Endpoint uses `.RequireAuthorization()` to enforce authentication
- Token must be valid and not expired

### Authorization
- **Resource Ownership:** User can only delete their own analyses
- Ownership check: `analysis.UserId == command.UserId`
- Return 403 Forbidden if ownership check fails (not 404 to prevent information disclosure)

### Input Validation
- Path parameter `analysisId` validated by ASP.NET Core routing (must be valid GUID)
- No user-provided body content (DELETE operation)

### Security Logging
- Log successful deletions with `AnalysisId` and `UserId` for audit trail
- Log unauthorized access attempts with warning level including attempted `AnalysisId`, requesting `UserId`, and actual owner `UserId`
- Avoid logging sensitive data

### Additional Security
- **HTTPS Required:** All API endpoints must use HTTPS in production
- **CORS:** Configure CORS appropriately for Blazor client
- **Rate Limiting:** Consider rate limiting DELETE operations to prevent abuse (e.g., 10 deletions per minute per user)

## 7. Error Handling

| Scenario | Status Code | Response Body |
|----------|-------------|---------------|
| Analysis not found | 404 | `{ "title": "Analysis not found", "detail": "The requested feed analysis does not exist", "status": 404 }` |
| User not authenticated | 401 | Challenge response (no body) |
| User doesn't own analysis | 403 | `{ "title": "Forbidden", "detail": "You don't have permission to delete this analysis", "status": 403 }` |
| Analysis status is completed | 422 | `{ "title": "Cannot cancel completed analysis", "detail": "Only pending or in-progress analyses can be deleted. Completed, failed, and superseded analyses are preserved as historical records.", "status": 422 }` |
| Analysis status is failed | 422 | Same as above |
| Analysis status is superseded | 422 | Same as above |
| Database error | 503 | `{ "title": "Service unavailable", "detail": "An error occurred while deleting the analysis", "status": 503 }` |

**Error Handling Strategy:**
- Use try-catch in service layer to catch `DbUpdateException` and other database errors
- Return appropriate error codes via `DeleteFeedAnalysisResult`
- Log all errors with sufficient context (AnalysisId, UserId, exception details)
- Return user-friendly error messages without exposing internal implementation details

## 8. Performance Considerations

### Database Query Optimization
- Use single query to fetch analysis by primary key (indexed, O(log n))
- Use `AsNoTracking()` for read-only query when checking status (if separating check from delete)
- OR: Use tracked entity and call `Remove()` directly (simpler, single query)
- Query plan: Single row lookup by primary key = very fast

### Indexing
- Primary key `Id` is automatically indexed (used for lookup)
- No additional indexes needed for this operation

### Caching
- No caching needed (DELETE operation, not read-heavy)
- Consider cache invalidation if analysis data is cached elsewhere

### Transaction Handling
- `SaveChangesAsync()` runs within implicit transaction
- No explicit transaction needed for single-entity deletion

### Potential Bottlenecks
- **Database connection:** Use connection pooling (default in EF Core)
- **Concurrency:** If same analysis deleted concurrently, one request will fail with 404 (acceptable)
- **Background job cancellation:** When implemented, ensure job cancellation is fast or async to avoid blocking DELETE request

### Scalability
- Operation is lightweight (single row delete)
- No N+1 queries or cross-table joins
- Scales well with user count (isolated by UserId)

## 9. Implementation Steps

### Step 1: Create Service Layer Types
**Files to create:**
- `src/RSSVibe.Services/FeedAnalyses/DeleteFeedAnalysisCommand.cs`
- `src/RSSVibe.Services/FeedAnalyses/DeleteFeedAnalysisResult.cs`

**Files to modify:**
- `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisError.cs` - Add `CannotCancelCompletedAnalysis` enum value
- `src/RSSVibe.Services/FeedAnalyses/IFeedAnalysisService.cs` - Add method signature

### Step 2: Implement Service Layer
**File to modify:**
- `src/RSSVibe.Services/FeedAnalyses/FeedAnalysisService.cs`

**Implementation outline:**
```csharp
public async Task<DeleteFeedAnalysisResult> DeleteFeedAnalysisAsync(
    DeleteFeedAnalysisCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        var analysis = await dbContext.FeedAnalyses
            .FirstOrDefaultAsync(a => a.Id == command.AnalysisId, cancellationToken);

        if (analysis is null)
        {
            return DeleteFeedAnalysisResult.Failed(FeedAnalysisError.NotFound);
        }

        if (analysis.UserId != command.UserId)
        {
            logger.LogWarning(
                "Unauthorized deletion attempt for analysis {AnalysisId} by user {UserId}. Owner is {OwnerId}",
                command.AnalysisId, command.UserId, analysis.UserId);
            return DeleteFeedAnalysisResult.Failed(FeedAnalysisError.Unauthorized);
        }

        // Only allow deletion of pending or in-progress analyses
        if (analysis.AnalysisStatus is not (FeedAnalysisStatus.Pending or FeedAnalysisStatus.InProgress))
        {
            return DeleteFeedAnalysisResult.Failed(
                FeedAnalysisError.CannotCancelCompletedAnalysis,
                "Only pending or in-progress analyses can be deleted. Completed, failed, and superseded analyses are preserved as historical records.");
        }

        dbContext.FeedAnalyses.Remove(analysis);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Feed analysis deleted: {AnalysisId} by user {UserId}",
            command.AnalysisId, command.UserId);

        // TODO: Cancel background job when job system is implemented

        return DeleteFeedAnalysisResult.Succeeded();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting feed analysis {AnalysisId} for user {UserId}",
            command.AnalysisId, command.UserId);
        return DeleteFeedAnalysisResult.Failed(FeedAnalysisError.DatabaseError);
    }
}
```

### Step 3: Create Endpoint
**File to create:**
- `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/DeleteFeedAnalysisEndpoint.cs`

**Implementation outline:**
```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Services.FeedAnalyses;
using System.Security.Claims;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class DeleteFeedAnalysisEndpoint
{
    public static RouteGroupBuilder MapDeleteFeedAnalysisEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{analysisId:guid}", HandleAsync)
            .WithName("DeleteFeedAnalysis")
            .WithSummary("Delete a feed analysis")
            .WithDescription("Cancels and deletes a pending or in-progress feed analysis. Completed, failed, and superseded analyses are preserved as historical records.")
            .RequireAuthorization();

        return group;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        Guid analysisId,
        ClaimsPrincipal user,
        IFeedAnalysisService feedAnalysisService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var command = new DeleteFeedAnalysisCommand(analysisId, userId);
        var result = await feedAnalysisService.DeleteFeedAnalysisAsync(command, cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.NotFound => TypedResults.NotFound(),
                FeedAnalysisError.Unauthorized => TypedResults.Problem(
                    title: "Forbidden",
                    detail: "You don't have permission to delete this analysis",
                    statusCode: StatusCodes.Status403Forbidden),
                FeedAnalysisError.CannotCancelCompletedAnalysis => TypedResults.Problem(
                    title: "Cannot cancel completed analysis",
                    detail: result.ErrorDetail ?? "Only pending or in-progress analyses can be deleted",
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => TypedResults.Problem(
                    title: "Failed to delete analysis",
                    detail: result.ErrorDetail,
                    statusCode: StatusCodes.Status503ServiceUnavailable)
            };
        }

        return TypedResults.NoContent();
    }
}
```

### Step 4: Register Endpoint
**File to modify:**
- `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/FeedAnalysesGroup.cs`

**Add registration:**
```csharp
group.MapDeleteFeedAnalysisEndpoint();
```

### Step 5: Create API Client Methods
**Files to modify:**
- `src/RSSVibe.Contracts/IFeedAnalysesClient.cs` - Add interface method
- `src/RSSVibe.Contracts/Internal/FeedAnalysesClient.cs` - Add implementation

See "API Client Interface" section above for code.

### Step 6: Add Integration Tests
**File to create:**
- `Tests/RSSVibe.ApiService.Tests/Endpoints/FeedAnalyses/DeleteFeedAnalysisEndpointTests.cs`

**Test scenarios to cover:**
1. `DeleteFeedAnalysis_WithValidPendingAnalysis_ShouldReturn204()` - Happy path
2. `DeleteFeedAnalysis_WithValidInProgressAnalysis_ShouldReturn204()` - Happy path
3. `DeleteFeedAnalysis_WithCompletedAnalysis_ShouldReturn422()` - Status constraint
4. `DeleteFeedAnalysis_WithFailedAnalysis_ShouldReturn422()` - Status constraint
5. `DeleteFeedAnalysis_WithSupersededAnalysis_ShouldReturn422()` - Status constraint
6. `DeleteFeedAnalysis_WithNonExistentAnalysis_ShouldReturn404()` - Not found
7. `DeleteFeedAnalysis_WithOtherUsersAnalysis_ShouldReturn403()` - Ownership check
8. `DeleteFeedAnalysis_WithoutAuthentication_ShouldReturn401()` - Auth check
9. `DeleteFeedAnalysis_WithInvalidGuid_ShouldReturn400()` - Invalid path parameter
10. `DeleteFeedAnalysis_WithLinkedFeed_ShouldSetFeedAnalysisIdToNull()` - Cascade behavior

**Use typed API client pattern:**
```csharp
// DO NOT use HttpClient.DeleteAsync() directly
// USE typed client instead:
var apiClient = scope.ServiceProvider.GetRequiredService<IRSSVibeApiClient>();
var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, cancellationToken);
```

### Step 7: Update Documentation
**File to modify:**
- `src/RSSVibe.ApiService/Endpoints/FeedAnalyses/FeedAnalysesGroup.cs`

Ensure OpenAPI metadata is set correctly via `.WithSummary()` and `.WithDescription()` (already done in endpoint registration).

---

## Implementation Checklist

- [ ] Step 1: Create `DeleteFeedAnalysisCommand.cs` and `DeleteFeedAnalysisResult.cs`
- [ ] Step 2: Update `FeedAnalysisError.cs` with new error value
- [ ] Step 3: Add method signature to `IFeedAnalysisService.cs`
- [ ] Step 4: Implement `DeleteFeedAnalysisAsync()` in `FeedAnalysisService.cs`
- [ ] Step 5: Create `DeleteFeedAnalysisEndpoint.cs`
- [ ] Step 6: Register endpoint in `FeedAnalysesGroup.cs`
- [ ] Step 7: Add `DeleteAsync()` to `IFeedAnalysesClient.cs`
- [ ] Step 8: Implement `DeleteAsync()` in `FeedAnalysesClient.cs`
- [ ] Step 9: Create `DeleteFeedAnalysisEndpointTests.cs` with all test scenarios
- [ ] Step 10: Run tests and verify all pass
- [ ] Step 11: Verify OpenAPI documentation generation
- [ ] Step 12: Manual testing with authenticated requests
