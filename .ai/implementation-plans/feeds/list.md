# API Endpoint Implementation Plan: GET /api/v1/feeds

## 1. Endpoint Overview

This endpoint retrieves a paginated list of feeds owned by the authenticated user. It supports filtering by parse status, scheduled time, and search text (matching title or URL). Results can be sorted by creation date, last parse time, or title. The endpoint uses indexed queries with `AsNoTracking()` for optimal read performance and returns feed metadata including schedule, cache state, and public RSS URL.

## 2. Request Details

- **HTTP Method**: GET
- **URL Structure**: `/api/v1/feeds`
- **Parameters**:
  - **Path**: None
  - **Query**:
    - `skip` (integer, ≥0, default 0): Pagination offset
    - `take` (integer, 1-50, default 20): Page size
    - `sort` (string, default "lastParsedAt:desc"): Sort field and direction (createdAt|lastParsedAt|title with :asc|:desc suffix)
    - `status` (string, optional): Filter by lastParseStatus (scheduled|running|succeeded|failed|skipped)
    - `nextParseBefore` (string, optional): ISO 8601 timestamp to filter feeds scheduled before this time
    - `search` (string, optional): Case-insensitive partial match on title or source URL
    - `includeInactive` (boolean, optional, default false): Include feeds with no recent activity
  - **Body**: None
- **Authentication**: Required (JWT Bearer Token)

## 3. Used Types

### Request Contracts

**Location**: `RSSVibe.Contracts/Feeds/ListFeedsRequest.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to list feeds for the authenticated user.
/// </summary>
public sealed record ListFeedsRequest(
    int Skip = 0,
    int Take = 20,
    string Sort = "lastParsedAt:desc",
    string? Status = null,
    string? NextParseBefore = null,
    string? Search = null,
    bool IncludeInactive = false
)
{
    public sealed class Validator : AbstractValidator<ListFeedsRequest>
    {
        private static readonly string[] ValidSortFields = ["createdAt", "lastParsedAt", "title"];
        private static readonly string[] ValidSortDirections = ["asc", "desc"];
        private static readonly string[] ValidStatuses = ["scheduled", "running", "succeeded", "failed", "skipped"];

        public Validator()
        {
            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Skip must be greater than or equal to 0.");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 50)
                .WithMessage("Take must be between 1 and 50.");

            RuleFor(x => x.Sort)
                .NotEmpty()
                .WithMessage("Sort is required.")
                .Must(s =>
                {
                    var parts = s.Split(':');
                    if (parts.Length != 2) return false;
                    return ValidSortFields.Contains(parts[0]) && ValidSortDirections.Contains(parts[1]);
                })
                .WithMessage("Sort must be in format 'field:direction' where field is 'createdAt', 'lastParsedAt', or 'title', and direction is 'asc' or 'desc'.");

            RuleFor(x => x.Status)
                .Must(s => s == null || ValidStatuses.Contains(s))
                .WithMessage("Status must be one of: scheduled, running, succeeded, failed, skipped.");

            RuleFor(x => x.NextParseBefore)
                .Must(BeValidIso8601Timestamp!)
                .WithMessage("NextParseBefore must be a valid ISO 8601 timestamp.")
                .When(x => x.NextParseBefore is not null);
        }

        private static bool BeValidIso8601Timestamp(string timestamp)
        {
            return DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out _);
        }
    }
}
```

### Response Contracts

**Location**: `RSSVibe.Contracts/Feeds/ListFeedsResponse.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response containing paginated list of feeds.
/// </summary>
public sealed record ListFeedsResponse(
    IReadOnlyList<FeedListItemDto> Items,
    PagingDto Paging
);
```

**Location**: `RSSVibe.Contracts/Feeds/FeedListItemDto.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Summary information for a feed in list view.
/// </summary>
public sealed record FeedListItemDto(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    int PendingParseCount,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RssUrl
);
```

**Location**: `RSSVibe.Contracts/Feeds/PagingDto.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Pagination metadata for list responses.
/// </summary>
public sealed record PagingDto(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);
```

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/ListFeedsQuery.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Query to list feeds for a user.
/// </summary>
public sealed record ListFeedsQuery(
    Guid UserId,
    int Skip,
    int Take,
    string SortField,
    string SortDirection,
    string? Status,
    DateTime? NextParseBefore,
    string? Search,
    bool IncludeInactive
);
```

**Location**: `RSSVibe.Services/Feeds/ListFeedsResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of listing feeds.
/// </summary>
public sealed record ListFeedsResult
{
    public bool Success { get; init; }
    public IReadOnlyList<FeedListItem>? Items { get; init; }
    public int TotalCount { get; init; }
    public FeedListError? Error { get; init; }
}

/// <summary>
/// Feed summary data for list view.
/// </summary>
public sealed record FeedListItem(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    string UpdateIntervalUnit,
    short UpdateIntervalValue,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    int PendingParseCount,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Location**: `RSSVibe.Services/Feeds/FeedListError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when listing feeds.
/// </summary>
public enum FeedListError
{
    DatabaseUnavailable
}
```

## 4. Response Details

### Success (200 OK)

**Headers**:
- `Cache-Control`: `private, max-age=60`

**Body**:
```json
{
  "items": [
    {
      "feedId": "uuid",
      "userId": "uuid",
      "sourceUrl": "https://example.com/news",
      "normalizedSourceUrl": "https://example.com/news",
      "title": "Example News",
      "description": "Latest updates from Example",
      "language": "en",
      "updateInterval": {
        "unit": "hour",
        "value": 1
      },
      "ttlMinutes": 60,
      "etag": "\"abc\"",
      "lastModified": "2024-05-01T12:00:00Z",
      "lastParsedAt": "2024-05-01T12:30:00Z",
      "nextParseAfter": "2024-05-01T13:30:00Z",
      "lastParseStatus": "succeeded",
      "pendingParseCount": 0,
      "analysisId": "uuid",
      "createdAt": "2024-05-01T11:00:00Z",
      "updatedAt": "2024-05-01T12:30:05Z",
      "rssUrl": "/feed/{userId}/{feedId}"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "totalCount": 12,
    "hasMore": false
  }
}
```

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 400 | Validation failures (invalid sort, skip < 0, take out of range) | `ValidationProblem` |
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 422 | Invalid timestamp format | `UnprocessableEntity` |
| 503 | Database unavailable | `ServiceUnavailable` |

## 5. Data Flow

1. **Endpoint receives request** with query parameters and extracts authenticated user ID from JWT claims.

2. **FluentValidation automatically validates** the request using `ListFeedsRequest.Validator` before handler executes. Returns `400 Bad Request` if validation fails.

3. **Handler maps request to query**:
   - Extract user ID from `ClaimsPrincipal`
   - Parse `Sort` parameter into `SortField` and `SortDirection`
   - Parse `NextParseBefore` ISO timestamp to `DateTime?`
   - Create `ListFeedsQuery` with all parameters

4. **Service builds filtered and sorted query**:
   - Start with `dbContext.Feeds.AsNoTracking()` for read-only query
   - Filter by `UserId == query.UserId`
   - If `Status` provided, filter by `LastParseStatus == query.Status`
   - If `NextParseBefore` provided, filter by `NextParseAfter < query.NextParseBefore`
   - If `Search` provided, filter by `Title.Contains(query.Search) || SourceUrl.Contains(query.Search)` (case-insensitive)
   - If `IncludeInactive == false`, filter by `LastParsedAt != null` or `CreatedAt > DateTime.UtcNow.AddDays(-7)`

5. **Service calculates pending parse count**:
   - Count of associated `FeedParseRuns` where `Status IN ('scheduled', 'running')`
   - Use left join or separate query to avoid N+1 issue

6. **Service applies sorting**:
   - Order by specified field and direction using indexed columns
   - Map `SortField` to entity property: `"createdAt"` → `CreatedAt`, `"lastParsedAt"` → `LastParsedAt`, `"title"` → `Title`
   - Apply `OrderBy` or `OrderByDescending` based on `SortDirection`

7. **Service executes paginated query**:
   - Count total matching records: `await query.CountAsync(ct)`
   - Apply pagination: `query.Skip(query.Skip).Take(query.Take)`
   - Execute query: `await query.ToListAsync(ct)`

8. **Service maps entities to result**:
   - Map each `Feed` entity to `FeedListItem` record
   - Calculate `PendingParseCount` for each feed
   - Return `ListFeedsResult` with items and total count

9. **Handler maps result to response**:
   - If `result.Error` is not null, return appropriate error status code
   - Map `FeedListItem` to `FeedListItemDto` (including constructing `UpdateIntervalDto` and `RssUrl`)
   - Create `PagingDto` with `HasMore = (skip + take < totalCount)`
   - Return `TypedResults.Ok` with `Cache-Control` header

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Implicit filtering**: Query filters by authenticated user's ID, ensuring users only see their own feeds
- No explicit authorization checks needed (data is scoped to user)

### Input Validation
- **FluentValidation** prevents:
  - Negative skip values
  - Take values outside 1-50 range
  - Invalid sort field/direction combinations
  - Invalid status values
  - Malformed ISO 8601 timestamps
- **Search parameter sanitization**:
  - EF Core parameterizes queries to prevent SQL injection
  - Limit search term length to prevent DoS (consider adding max length validation)

### Rate Limiting
- Consider rate limiting per user to prevent abuse (e.g., 100 requests per minute)
- Particularly important for expensive search queries

### Additional Security
- HTTPS required for all API communication
- `Cache-Control: private` ensures response isn't cached by shared proxies
- Don't leak sensitive information in error messages

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| Skip < 0 | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Skip": ["Skip must be greater than or equal to 0."] } }` |
| Take outside 1-50 range | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Take": ["Take must be between 1 and 50."] } }` |
| Invalid sort format | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Sort": ["Sort must be in format 'field:direction' where field is 'createdAt', 'lastParsedAt', or 'title', and direction is 'asc' or 'desc'."] } }` |
| Invalid status value | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Status": ["Status must be one of: scheduled, running, succeeded, failed, skipped."] } }` |
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Invalid ISO 8601 timestamp | 422 | `{ "type": "invalid_timestamp", "title": "Invalid timestamp format", "detail": "NextParseBefore must be a valid ISO 8601 timestamp." }` |
| Database timeout/unavailable | 503 | `{ "type": "service_unavailable", "title": "Service unavailable", "detail": "Unable to retrieve feeds at this time. Please try again later." }` |

## 8. Performance Considerations

### Database Query Optimization
- **AsNoTracking()**: Read-only queries don't need change tracking, reduces memory overhead
- **Indexed queries**:
  - Filter by UserId (foreign key index)
  - Sort by CreatedAt, LastParsedAt (btree indexes)
  - Filter by NextParseAfter + LastParseStatus (composite index)
- **Avoid N+1 queries** for pending parse count:
  - Option 1: Use `Include()` with filtered collection: `.Include(f => f.ParseRuns.Where(r => r.Status == "scheduled" || r.Status == "running"))`
  - Option 2: Separate aggregation query with GroupBy
  - Option 3: Use projection with subquery in SELECT

### Caching Strategy
- **Response caching** with FusionCache:
  - Cache key: `feeds:list:{userId}:{skip}:{take}:{sort}:{status}:{nextParseBefore}:{search}:{includeInactive}`
  - TTL: 60 seconds (matches Cache-Control header)
  - Invalidate on feed create/update/delete
- **Trade-off**: Caching may show stale data for high-frequency updates
- **Alternative**: Cache only expensive queries (with search filter)

### Pagination Approach
- **Offset pagination** (skip/take):
  - Simple to implement and understand
  - Works well with sorting
  - Performance degrades with large skip values (deep pagination)
- **Consider cursor-based pagination** for very large datasets in future

### Potential Bottlenecks
- **Large result sets**: Mitigated by enforcing max take = 50
- **Full-text search**: Search on Title/SourceUrl uses LIKE queries
  - Consider adding PostgreSQL full-text search index if search is heavily used
  - Use `ILIKE` for case-insensitive search, or trigram index for better performance
- **Pending parse count calculation**: Could be expensive if many parse runs
  - Optimize with indexed query or denormalized count column

## 9. Implementation Steps

### 1. Create Contract Types
**File**: `src/RSSVibe.Contracts/Feeds/ListFeedsRequest.cs`
- Define `ListFeedsRequest` positional record with default values
- Create nested `Validator` class with all validation rules
- Add helper method for ISO 8601 timestamp validation

**File**: `src/RSSVibe.Contracts/Feeds/ListFeedsResponse.cs`
- Define `ListFeedsResponse` positional record

**File**: `src/RSSVibe.Contracts/Feeds/FeedListItemDto.cs`
- Define `FeedListItemDto` positional record with all properties

**File**: `src/RSSVibe.Contracts/Feeds/PagingDto.cs`
- Define `PagingDto` positional record

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/ListFeedsQuery.cs`
- Define `ListFeedsQuery` positional record with parsed parameters

**File**: `src/RSSVibe.Services/Feeds/ListFeedsResult.cs`
- Define `ListFeedsResult` record with list of `FeedListItem`
- Define `FeedListItem` record

**File**: `src/RSSVibe.Services/Feeds/FeedListError.cs`
- Define `FeedListError` enum

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (update)
- Add method signature: `Task<ListFeedsResult> ListFeedsAsync(ListFeedsQuery query, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (update)
- Implement `ListFeedsAsync` method:
  1. Build base query with `AsNoTracking()` and UserId filter
  2. Apply optional filters (Status, NextParseBefore, Search, IncludeInactive)
  3. Calculate pending parse count (with separate query or projection)
  4. Apply sorting based on SortField and SortDirection
  5. Count total matching records
  6. Apply pagination (Skip/Take)
  7. Execute query and map to result

### 3. Implement Validation
Validator is already defined as nested class in `ListFeedsRequest` (step 1).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/ListFeedsEndpoint.cs`
- Create `ListFeedsEndpoint` static class
- Implement `MapListFeedsEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<Ok<ListFeedsResponse>, ValidationProblem, UnauthorizedHttpResult, UnprocessableEntity, ServiceUnavailable>>
      HandleAsync([AsParameters] ListFeedsRequest request, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Parse sort parameter into field and direction
- Parse NextParseBefore timestamp
- Map request to query
- Call service
- Map result to response with TypedResults.Ok and Cache-Control header
- Add OpenAPI metadata

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (update)
- Add call to `group.MapListFeedsEndpoint();` in `MapFeedsGroup()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/ListFeedsEndpointTests.cs`

Test scenarios:
1. `ListFeedsEndpoint_WithDefaultParameters_ShouldReturnPaginatedFeeds`
   - Arrange: Create 3 feeds for user, use default request params
   - Act: GET endpoint
   - Assert: 200 OK, 3 items, correct paging metadata

2. `ListFeedsEndpoint_WithSkipAndTake_ShouldReturnCorrectPage`
   - Arrange: Create 30 feeds, request skip=10, take=5
   - Act: GET endpoint
   - Assert: 200 OK, 5 items, correct paging (hasMore=true, totalCount=30)

3. `ListFeedsEndpoint_WithSortByTitleAsc_ShouldReturnSortedFeeds`
   - Arrange: Create feeds with different titles
   - Act: GET endpoint with sort=title:asc
   - Assert: 200 OK, items sorted alphabetically by title

4. `ListFeedsEndpoint_WithSortByLastParsedAtDesc_ShouldReturnSortedFeeds`
   - Arrange: Create feeds with different LastParsedAt values
   - Act: GET endpoint with sort=lastParsedAt:desc
   - Assert: 200 OK, items sorted by parse time descending

5. `ListFeedsEndpoint_WithStatusFilter_ShouldReturnOnlyMatchingFeeds`
   - Arrange: Create feeds with different LastParseStatus values
   - Act: GET endpoint with status=succeeded
   - Assert: 200 OK, only feeds with succeeded status

6. `ListFeedsEndpoint_WithSearchFilter_ShouldReturnMatchingFeeds`
   - Arrange: Create feeds with different titles
   - Act: GET endpoint with search=example
   - Assert: 200 OK, only feeds with "example" in title or URL

7. `ListFeedsEndpoint_WithNextParseBeforeFilter_ShouldReturnScheduledFeeds`
   - Arrange: Create feeds with different NextParseAfter values
   - Act: GET endpoint with nextParseBefore={future timestamp}
   - Assert: 200 OK, only feeds scheduled before the timestamp

8. `ListFeedsEndpoint_WithNegativeSkip_ShouldReturnValidationError`
   - Arrange: Request with skip=-1
   - Act: GET endpoint
   - Assert: 400 Bad Request

9. `ListFeedsEndpoint_WithTakeOutOfRange_ShouldReturnValidationError`
   - Arrange: Request with take=100
   - Act: GET endpoint
   - Assert: 400 Bad Request

10. `ListFeedsEndpoint_WithInvalidSortFormat_ShouldReturnValidationError`
    - Arrange: Request with sort=invalidField:desc
    - Act: GET endpoint
    - Assert: 400 Bad Request

11. `ListFeedsEndpoint_WithInvalidTimestamp_ShouldReturnUnprocessableEntity`
    - Arrange: Request with nextParseBefore=not-a-timestamp
    - Act: GET endpoint
    - Assert: 422 Unprocessable Entity

12. `ListFeedsEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
    - Arrange: Valid request, no auth token
    - Act: GET endpoint with unauthenticated client
    - Assert: 401 Unauthorized

13. `ListFeedsEndpoint_ShouldOnlyReturnFeedsForAuthenticatedUser`
    - Arrange: Create feeds for two different users
    - Act: GET endpoint with user1's token
    - Assert: 200 OK, only user1's feeds returned

14. `ListFeedsEndpoint_ShouldIncludePendingParseCount`
    - Arrange: Create feed with scheduled parse runs
    - Act: GET endpoint
    - Assert: 200 OK, pendingParseCount reflects scheduled/running parse runs

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/ListFeedsEndpoint.cs`
- Add XML documentation comments to handler method
- Add `.WithOpenApi()` configuration with detailed description, parameter descriptions, and response examples
