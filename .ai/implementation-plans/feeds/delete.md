# API Endpoint Implementation Plan: DELETE /api/v1/feeds/{feedId}

## 1. Endpoint Overview

This endpoint deletes a feed and cascades the deletion to all associated parse runs and items (as defined by database foreign key constraints). The authenticated user must own the feed. The endpoint prevents deletion if the feed has an active parse run in progress. Successful deletion returns `204 No Content`.

## 2. Request Details

- **HTTP Method**: DELETE
- **URL Structure**: `/api/v1/feeds/{feedId}`
- **Parameters**:
  - **Path**:
    - `feedId` (uuid, required): Unique identifier of the feed to delete
  - **Query**: None
  - **Body**: None
- **Authentication**: Required (JWT Bearer Token). User must own the feed.

## 3. Used Types

### Request Contracts

No request DTO needed (path parameter only).

### Response Contracts

No response body for successful deletion (204 No Content).

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/DeleteFeedCommand.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Command to delete a feed.
/// </summary>
public sealed record DeleteFeedCommand(
    Guid FeedId,
    Guid UserId
);
```

**Location**: `RSSVibe.Services/Feeds/DeleteFeedResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of deleting a feed.
/// </summary>
public sealed record DeleteFeedResult
{
    public bool Success { get; init; }
    public FeedDeleteError? Error { get; init; }
}
```

**Location**: `RSSVibe.Services/Feeds/FeedDeleteError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when deleting a feed.
/// </summary>
public enum FeedDeleteError
{
    FeedNotFound,
    FeedNotOwned,
    ParseRunInProgress,
    SchedulerUnavailable
}
```

## 4. Response Details

### Success (204 No Content)

No response body.

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 403 | Feed belongs to different user | `ForbidHttpResult` |
| 404 | Feed not found | `NotFound` |
| 409 | Parse run currently in progress | `Conflict` |
| 503 | Scheduler unavailable | `ServiceUnavailable` |

## 5. Data Flow

1. **Endpoint receives request** with `feedId` path parameter and extracts authenticated user ID from JWT claims.

2. **Handler creates command**:
   - Extract user ID from `ClaimsPrincipal`
   - Create `DeleteFeedCommand` with feedId and userId

3. **Service queries and validates feed**:
   - Query `Feeds` table with `Include(f => f.ParseRuns)` to load active parse runs
   - Filter by `Id == command.FeedId`
   - Return `FeedDeleteError.FeedNotFound` if not found
   - Verify `feed.UserId == command.UserId` (return `FeedDeleteError.FeedNotOwned` if not)

4. **Service checks for active parse runs**:
   - Query associated `FeedParseRuns` for any with `Status IN ('scheduled', 'running')`
   - Return `FeedDeleteError.ParseRunInProgress` if any found

5. **Service deletes feed using execution strategy with transaction**:
   - Remove feed entity from `DbContext.Feeds`
   - Database cascades delete to:
     - `FeedParseRuns` (via FK on delete cascade)
     - `FeedItems` (via FK on delete cascade)
     - `FeedParseRunItems` (via FK on delete cascade from parse runs and items)
   - Update `FeedAnalysis.ApprovedFeedId = null` if feed was linked to analysis (set null on delete)
   - Commit transaction

6. **Service returns success result**.

7. **Handler maps result to response**:
   - If `result.Error` is not null, map to appropriate HTTP status code
   - Otherwise, return `TypedResults.NoContent()`

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Resource ownership**: Verify `feed.UserId == authenticated user ID`
- Return `403 Forbidden` if user doesn't own the feed

### Data Integrity
- **Cascade deletion**: Database foreign keys handle related data cleanup
- **Prevent deletion during active parsing**: Avoids data inconsistency and orphaned parse runs
- Use **execution strategy with transaction** to ensure atomicity

### Audit Trail
- **Log deletion events** with user ID, feed ID, and timestamp for audit purposes
- Consider soft delete pattern in future (add `DeletedAt` column) to preserve history

### Additional Security
- HTTPS required for all API communication
- Log deletion attempts for security monitoring

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Feed belongs to different user | 403 | `TypedResults.Forbid()` |
| Feed not found | 404 | `TypedResults.NotFound()` |
| Parse run in progress | 409 | `{ "type": "parse_in_progress", "title": "Cannot delete feed", "detail": "The feed has an active parse run in progress. Please wait for it to complete before deleting." }` |
| Scheduler unavailable | 503 | `{ "type": "service_unavailable", "title": "Service unavailable", "detail": "Unable to delete feed at this time. Please try again later." }` |

## 8. Performance Considerations

### Database Query Optimization
- **Primary key lookup with Include()**: Load feed and active parse runs in single query
- **Cascading deletes**: Database handles related data cleanup efficiently with foreign key constraints
- **Transaction with execution strategy**: Required for PostgreSQL, ensures atomicity

### Caching Strategy
- **Invalidate cached feed detail** after successful deletion:
  - Cache key: `feed:detail:{feedId}`
- **Invalidate cached feed list** for user:
  - Cache key: `feeds:list:{userId}:*` (wildcard invalidation)

### Potential Bottlenecks
- **Large number of related records**: Cascading delete could be slow for feeds with thousands of items/parse runs
  - Mitigated by database-level cascade (faster than application-level deletion)
  - Consider background job for large deletions if this becomes an issue

## 9. Implementation Steps

### 1. Create Contract Types

No contract types needed for this endpoint.

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/DeleteFeedCommand.cs`
- Define `DeleteFeedCommand` positional record

**File**: `src/RSSVibe.Services/Feeds/DeleteFeedResult.cs`
- Define `DeleteFeedResult` record

**File**: `src/RSSVibe.Services/Feeds/FeedDeleteError.cs`
- Define `FeedDeleteError` enum

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (update)
- Add method signature: `Task<DeleteFeedResult> DeleteFeedAsync(DeleteFeedCommand command, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (update)
- Implement `DeleteFeedAsync` method:
  1. Query feed by ID with Include for parse runs
  2. Validate ownership
  3. Check for active parse runs
  4. Use execution strategy with transaction
  5. Remove feed entity
  6. Commit transaction
  7. Return result

### 3. Implement Validation

No custom validation needed (UUID format validated by ASP.NET Core).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/DeleteFeedEndpoint.cs`
- Create `DeleteFeedEndpoint` static class
- Implement `MapDeleteFeedEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, NotFound, Conflict, ServiceUnavailable>>
      HandleAsync(Guid feedId, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Create command
- Call service
- Map result to response with TypedResults
- Add OpenAPI metadata

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (update)
- Add call to `group.MapDeleteFeedEndpoint();` in `MapFeedsGroup()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/DeleteFeedEndpointTests.cs`

Test scenarios:
1. `DeleteFeedEndpoint_WithValidFeedId_ShouldDeleteFeedAndReturn204`
   - Arrange: Create feed for authenticated user
   - Act: DELETE /feeds/{feedId}
   - Assert: 204 No Content, feed no longer exists in database

2. `DeleteFeedEndpoint_ShouldCascadeDeleteParseRuns`
   - Arrange: Create feed with parse runs
   - Act: DELETE /feeds/{feedId}
   - Assert: 204 No Content, parse runs deleted from database

3. `DeleteFeedEndpoint_ShouldCascadeDeleteFeedItems`
   - Arrange: Create feed with feed items
   - Act: DELETE /feeds/{feedId}
   - Assert: 204 No Content, feed items deleted from database

4. `DeleteFeedEndpoint_ShouldUnlinkFromAnalysis`
   - Arrange: Create feed linked to analysis (ApprovedFeedId set)
   - Act: DELETE /feeds/{feedId}
   - Assert: 204 No Content, analysis.ApprovedFeedId set to null

5. `DeleteFeedEndpoint_WithActiveParseRun_ShouldReturnConflict`
   - Arrange: Create feed with running parse run
   - Act: DELETE /feeds/{feedId}
   - Assert: 409 Conflict

6. `DeleteFeedEndpoint_WithNonExistentFeedId_ShouldReturnNotFound`
   - Arrange: Random UUID
   - Act: DELETE /feeds/{randomId}
   - Assert: 404 Not Found

7. `DeleteFeedEndpoint_WithFeedBelongingToDifferentUser_ShouldReturnForbidden`
   - Arrange: Create feed for user2, authenticate as user1
   - Act: DELETE /feeds/{user2FeedId}
   - Assert: 403 Forbidden

8. `DeleteFeedEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
   - Arrange: Create feed
   - Act: DELETE /feeds/{feedId} without auth token
   - Assert: 401 Unauthorized

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/DeleteFeedEndpoint.cs`
- Add XML documentation comments
- Add `.WithOpenApi()` configuration with detailed description and status codes
