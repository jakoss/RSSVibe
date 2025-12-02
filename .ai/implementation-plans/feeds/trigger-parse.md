# API Endpoint Implementation Plan: POST /api/v1/feeds/{feedId}/trigger-parse

## 1. Endpoint Overview

This endpoint manually enqueues an immediate parse run for a feed, bypassing the scheduled interval. It is useful for testing feed configurations or forcing an update after selector changes. The endpoint enforces rate limiting to prevent abuse (cooldown period between manual triggers) and checks for existing queued/running parse runs to avoid duplicates. Successful invocation returns `202 Accepted` with the new parse run ID.

## 2. Request Details

- **HTTP Method**: POST
- **URL Structure**: `/api/v1/feeds/{feedId}/trigger-parse`
- **Parameters**:
  - **Path**:
    - `feedId` (uuid, required): Unique identifier of the feed to parse
  - **Query**: None
  - **Body**: None
- **Authentication**: Required (JWT Bearer Token). User must own the feed.

## 3. Used Types

### Request Contracts

No request DTO needed (path parameter only).

### Response Contracts

**Location**: `RSSVibe.Contracts/Feeds/TriggerParseResponse.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response after manually triggering a parse run.
/// </summary>
public sealed record TriggerParseResponse(
    Guid FeedId,
    Guid ParseRunId,
    string Status
);
```

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/TriggerParseCommand.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Command to manually trigger a parse run.
/// </summary>
public sealed record TriggerParseCommand(
    Guid FeedId,
    Guid UserId
);
```

**Location**: `RSSVibe.Services/Feeds/TriggerParseResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of triggering a parse run.
/// </summary>
public sealed record TriggerParseResult
{
    public bool Success { get; init; }
    public Guid ParseRunId { get; init; }
    public string? Status { get; init; }
    public TriggerParseError? Error { get; init; }
}
```

**Location**: `RSSVibe.Services/Feeds/TriggerParseError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when triggering a parse run.
/// </summary>
public enum TriggerParseError
{
    FeedNotFound,
    FeedNotOwned,
    ParseAlreadyQueued,
    RateLimitExceeded,
    ParserUnavailable
}
```

## 4. Response Details

### Success (202 Accepted)

**Headers**:
- `Location`: `/api/v1/feeds/{feedId}/parse-runs/{parseRunId}`

**Body**:
```json
{
  "feedId": "uuid",
  "parseRunId": "uuid",
  "status": "scheduled"
}
```

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 403 | Feed belongs to different user | `ForbidHttpResult` |
| 404 | Feed not found | `NotFound` |
| 409 | Parse already queued or running | `Conflict` |
| 429 | Manual trigger rate limit exceeded (cooldown period) | `TooManyRequests` |
| 503 | Parser service offline or unavailable | `ServiceUnavailable` |

## 5. Data Flow

1. **Endpoint receives request** with `feedId` path parameter and extracts authenticated user ID from JWT claims.

2. **Handler creates command**:
   - Extract user ID from `ClaimsPrincipal`
   - Create `TriggerParseCommand` with feedId and userId

3. **Service queries and validates feed**:
   - Query `Feeds` table for feed with `Id == command.FeedId`
   - Return `TriggerParseError.FeedNotFound` if not found
   - Verify `feed.UserId == command.UserId` (return `TriggerParseError.FeedNotOwned` if not)

4. **Service checks for existing queued/running parse runs**:
   - Query `FeedParseRuns` where `FeedId == command.FeedId` AND `Status IN ('scheduled', 'running')`
   - Return `TriggerParseError.ParseAlreadyQueued` if any found

5. **Service enforces rate limiting**:
   - Check FusionCache for recent manual trigger timestamp: `manual-trigger:{feedId}`
   - If found and within cooldown period (e.g., 5 minutes), return `TriggerParseError.RateLimitExceeded`
   - Otherwise, continue

6. **Service creates parse run using execution strategy with transaction**:
   - Generate new UUIDv7 for parse run ID: `Guid.CreateVersion7()`
   - Create `FeedParseRun` entity:
     - `Id = parseRunId`
     - `FeedId = command.FeedId`
     - `StartedAt = DateTime.UtcNow`
     - `Status = FeedParseStatus.Scheduled`
     - Initialize counters to 0
   - Add to `DbContext.FeedParseRuns`
   - Save changes and commit transaction

7. **Service enqueues parse job**:
   - Publish message to background job queue (TickerQ or similar) with feedId and parseRunId
   - If queue unavailable, rollback transaction and return `TriggerParseError.ParserUnavailable`

8. **Service sets rate limit cache entry**:
   - Store timestamp in FusionCache with key `manual-trigger:{feedId}` and TTL of 5 minutes
   - This prevents subsequent manual triggers within the cooldown period

9. **Service returns success result** with parseRunId and status.

10. **Handler maps result to response**:
    - If `result.Error` is not null, map to appropriate HTTP status code
    - Otherwise, create `TriggerParseResponse` and return `TypedResults.Accepted` with `Location` header

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Resource ownership**: Verify `feed.UserId == authenticated user ID`
- Return `403 Forbidden` if user doesn't own the feed

### Rate Limiting
- **Per-feed cooldown**: 5 minutes between manual triggers
- **Purpose**: Prevent abuse, protect parser infrastructure
- **Implementation**: FusionCache with TTL-based expiration
- **Consider**: Per-user global rate limit (e.g., 10 manual triggers per hour across all feeds)

### Input Validation
- **Path parameter validation**: ASP.NET Core validates UUID format automatically

### Data Integrity
- **Prevent duplicate runs**: Check for existing scheduled/running parse runs before creating new one
- Use **execution strategy with transaction** to ensure atomicity

### Additional Security
- HTTPS required for all API communication
- Log manual trigger events for security monitoring and abuse detection

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Feed belongs to different user | 403 | `TypedResults.Forbid()` |
| Feed not found | 404 | `TypedResults.NotFound()` |
| Parse already queued/running | 409 | `{ "type": "parse_already_queued", "title": "Parse already queued", "detail": "A parse run is already scheduled or running for this feed." }` |
| Rate limit exceeded | 429 | `{ "type": "rate_limit_exceeded", "title": "Too many requests", "detail": "Manual trigger cooldown period has not elapsed. Please wait 5 minutes between manual triggers.", "retryAfter": 300 }` |
| Parser unavailable | 503 | `{ "type": "parser_unavailable", "title": "Parser unavailable", "detail": "Unable to enqueue parse run at this time. Please try again later." }` |

## 8. Performance Considerations

### Database Query Optimization
- **Primary key lookup**: Query by feedId uses primary key index
- **Check for existing runs**: Query with composite index on (FeedId, Status)
- **Transaction with execution strategy**: Required for PostgreSQL, ensures atomicity

### Caching Strategy
- **Rate limiting cache**: FusionCache entry for cooldown tracking
  - Cache key: `manual-trigger:{feedId}`
  - TTL: 5 minutes (cooldown period)
  - Store: Timestamp of last manual trigger

### Background Job Integration
- **TickerQ integration**: Publish parse job message to queue
- **Asynchronous processing**: Endpoint returns immediately after enqueuing, actual parsing happens in background
- **Resilience**: Handle queue unavailability gracefully with error response

### Potential Bottlenecks
- **Queue congestion**: If many manual triggers occur simultaneously, queue could become backlogged
  - Mitigated by rate limiting
- **Database contention**: Multiple concurrent triggers could cause lock contention
  - Mitigated by execution strategy and short transaction duration

## 9. Implementation Steps

### 1. Create Contract Types
**File**: `src/RSSVibe.Contracts/Feeds/TriggerParseResponse.cs`
- Define `TriggerParseResponse` positional record

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/TriggerParseCommand.cs`
- Define `TriggerParseCommand` positional record

**File**: `src/RSSVibe.Services/Feeds/TriggerParseResult.cs`
- Define `TriggerParseResult` record

**File**: `src/RSSVibe.Services/Feeds/TriggerParseError.cs`
- Define `TriggerParseError` enum

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (update)
- Add method signature: `Task<TriggerParseResult> TriggerParseAsync(TriggerParseCommand command, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (update)
- Implement `TriggerParseAsync` method:
  1. Query feed by ID
  2. Validate ownership
  3. Check for existing queued/running parse runs
  4. Check rate limit cache
  5. Use execution strategy with transaction to create parse run
  6. Enqueue background job (TickerQ)
  7. Set rate limit cache entry
  8. Return result with parseRunId

### 3. Implement Validation

No custom validation needed (UUID format validated by ASP.NET Core).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/TriggerParseEndpoint.cs`
- Create `TriggerParseEndpoint` static class
- Implement `MapTriggerParseEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<Accepted<TriggerParseResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound, Conflict, StatusCodeHttpResult, ServiceUnavailable>>
      HandleAsync(Guid feedId, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Create command
- Call service
- Map result to response with TypedResults.Accepted and Location header
- For 429 response, use `TypedResults.StatusCode(429)` with custom problem details including `retryAfter`
- Add OpenAPI metadata

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (update)
- Add call to `group.MapTriggerParseEndpoint();` in `MapFeedsGroup()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/TriggerParseEndpointTests.cs`

Test scenarios:
1. `TriggerParseEndpoint_WithValidFeedId_ShouldCreateParseRunAndReturn202`
   - Arrange: Create feed for authenticated user
   - Act: POST /feeds/{feedId}/trigger-parse
   - Assert: 202 Accepted, parseRunId returned, parse run exists in database with status=scheduled

2. `TriggerParseEndpoint_ShouldReturnLocationHeader`
   - Arrange: Create feed
   - Act: POST /feeds/{feedId}/trigger-parse
   - Assert: 202 Accepted, Location header = /api/v1/feeds/{feedId}/parse-runs/{parseRunId}

3. `TriggerParseEndpoint_WithExistingQueuedRun_ShouldReturnConflict`
   - Arrange: Create feed with scheduled parse run
   - Act: POST /feeds/{feedId}/trigger-parse
   - Assert: 409 Conflict

4. `TriggerParseEndpoint_WithExistingRunningRun_ShouldReturnConflict`
   - Arrange: Create feed with running parse run
   - Act: POST /feeds/{feedId}/trigger-parse
   - Assert: 409 Conflict

5. `TriggerParseEndpoint_WithinCooldownPeriod_ShouldReturnTooManyRequests`
   - Arrange: Create feed, trigger parse, wait < 5 minutes
   - Act: POST /feeds/{feedId}/trigger-parse (second time)
   - Assert: 429 Too Many Requests

6. `TriggerParseEndpoint_AfterCooldownPeriod_ShouldSucceed`
   - Arrange: Create feed, set cache timestamp to > 5 minutes ago (or mock cache)
   - Act: POST /feeds/{feedId}/trigger-parse
   - Assert: 202 Accepted

7. `TriggerParseEndpoint_WithNonExistentFeedId_ShouldReturnNotFound`
   - Arrange: Random UUID
   - Act: POST /feeds/{randomId}/trigger-parse
   - Assert: 404 Not Found

8. `TriggerParseEndpoint_WithFeedBelongingToDifferentUser_ShouldReturnForbidden`
   - Arrange: Create feed for user2, authenticate as user1
   - Act: POST /feeds/{user2FeedId}/trigger-parse
   - Assert: 403 Forbidden

9. `TriggerParseEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
   - Arrange: Create feed
   - Act: POST /feeds/{feedId}/trigger-parse without auth token
   - Assert: 401 Unauthorized

10. `TriggerParseEndpoint_ShouldSetRateLimitCacheEntry`
    - Arrange: Create feed, mock FusionCache
    - Act: POST /feeds/{feedId}/trigger-parse
    - Assert: 202 Accepted, cache entry created with 5-minute TTL

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/TriggerParseEndpoint.cs`
- Add XML documentation comments
- Add `.WithOpenApi()` configuration with detailed description, rate limiting information, and status codes
