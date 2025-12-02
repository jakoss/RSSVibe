# API Endpoint Implementation Plan: GET /api/v1/feeds/{feedId}

## 1. Endpoint Overview

This endpoint retrieves detailed information about a specific feed, including full selector configuration, schedule settings, HTTP cache metadata, and parse history. The authenticated user must own the requested feed. The response includes all configuration data needed for feed management UI and public RSS generation.

## 2. Request Details

- **HTTP Method**: GET
- **URL Structure**: `/api/v1/feeds/{feedId}`
- **Parameters**:
  - **Path**:
    - `feedId` (uuid, required): Unique identifier of the feed
  - **Query**: None
  - **Body**: None
- **Authentication**: Required (JWT Bearer Token). User must own the feed.

## 3. Used Types

### Request Contracts

No dedicated request DTO needed (path parameter only).

### Response Contracts

**Location**: `RSSVibe.Contracts/Feeds/FeedDetailResponse.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Detailed information about a feed.
/// </summary>
public sealed record FeedDetailResponse(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    FeedSelectorsDto Selectors,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RssUrl,
    CacheHeadersDto? CacheHeaders
);
```

**Location**: `RSSVibe.Contracts/Feeds/CacheHeadersDto.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// HTTP cache headers for the feed.
/// </summary>
public sealed record CacheHeadersDto(
    string? CacheControl,
    string? Etag,
    string? LastModified
);
```

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/GetFeedDetailQuery.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Query to get detailed feed information.
/// </summary>
public sealed record GetFeedDetailQuery(
    Guid FeedId,
    Guid UserId
);
```

**Location**: `RSSVibe.Services/Feeds/GetFeedDetailResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of getting feed details.
/// </summary>
public sealed record GetFeedDetailResult
{
    public bool Success { get; init; }
    public FeedDetail? Feed { get; init; }
    public FeedDetailError? Error { get; init; }
}

/// <summary>
/// Detailed feed data.
/// </summary>
public sealed record FeedDetail(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    FeedSelectors Selectors,
    string UpdateIntervalUnit,
    short UpdateIntervalValue,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Location**: `RSSVibe.Services/Feeds/FeedDetailError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when getting feed details.
/// </summary>
public enum FeedDetailError
{
    FeedNotFound,
    FeedNotOwned
}
```

## 4. Response Details

### Success (200 OK)

**Headers**:
- `Cache-Control`: `private, max-age=300`
- `ETag`: Computed from feed's UpdatedAt timestamp

**Body**:
```json
{
  "feedId": "uuid",
  "userId": "uuid",
  "sourceUrl": "https://example.com/news",
  "normalizedSourceUrl": "https://example.com/news",
  "title": "Example News",
  "description": "Latest updates from Example",
  "language": "en",
  "selectors": {
    "list": ".article-list",
    "item": ".article",
    "title": ".article-title",
    "link": ".article-link",
    "published": ".article-date"
  },
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
  "analysisId": "uuid",
  "createdAt": "2024-05-01T11:00:00Z",
  "updatedAt": "2024-05-01T12:30:05Z",
  "rssUrl": "/feed/{userId}/{feedId}",
  "cacheHeaders": {
    "cacheControl": "public, max-age=300",
    "etag": "\"abc\"",
    "lastModified": "Wed, 01 May 2024 12:00:00 GMT"
  }
}
```

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 403 | Feed belongs to different user | `ForbidHttpResult` |
| 404 | Feed not found | `NotFound` |

## 5. Data Flow

1. **Endpoint receives request** with `feedId` path parameter and extracts authenticated user ID from JWT claims.

2. **Handler validates feedId format**:
   - ASP.NET Core automatically parses UUID from path parameter
   - Invalid UUID format returns 400 Bad Request automatically

3. **Handler creates query**:
   - Extract user ID from `ClaimsPrincipal`
   - Create `GetFeedDetailQuery` with feedId and userId

4. **Service queries database**:
   - Query `Feeds` table with `AsNoTracking()` for read-only operation
   - Filter by `Id == query.FeedId`
   - Use `FirstOrDefaultAsync()` to get single result

5. **Service validates ownership**:
   - If feed not found, return `FeedDetailError.FeedNotFound`
   - If `feed.UserId != query.UserId`, return `FeedDetailError.FeedNotOwned`

6. **Service maps entity to result**:
   - Map `Feed` entity to `FeedDetail` record
   - Include all properties including JSON selectors
   - Return successful result

7. **Handler computes cache headers**:
   - Generate `ETag` from feed's `UpdatedAt` timestamp
   - Build `CacheHeadersDto` for public RSS feed consumption

8. **Handler maps result to response**:
   - If `result.Error == FeedNotFound`, return `TypedResults.NotFound()`
   - If `result.Error == FeedNotOwned`, return `TypedResults.Forbid()`
   - Map `FeedDetail` to `FeedDetailResponse`
   - Construct `RssUrl` as `/feed/{userId}/{feedId}`
   - Return `TypedResults.Ok` with `Cache-Control` and `ETag` headers

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Resource ownership**: Verify `feed.UserId == authenticated user ID`
- Return `403 Forbidden` if user doesn't own the feed
- This prevents users from accessing other users' feed configurations

### Input Validation
- **Path parameter validation**: ASP.NET Core validates UUID format automatically
- No additional validation needed for this endpoint

### Data Exposure
- **Sensitive data**: Feed selectors and source URLs are user-specific configuration
- **Cache control**: Use `private` to prevent shared proxy caching
- **Public RSS URL**: Exposed in response but requires separate public endpoint to access

### Additional Security
- HTTPS required for all API communication
- Log access attempts for security monitoring (especially 403 responses)

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| Invalid UUID format | 400 | Automatic ASP.NET Core validation error |
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Feed belongs to different user | 403 | `TypedResults.Forbid()` |
| Feed not found | 404 | `TypedResults.NotFound()` or `{ "type": "feed_not_found", "title": "Feed not found", "detail": "The specified feed does not exist." }` |

## 8. Performance Considerations

### Database Query Optimization
- **AsNoTracking()**: Read-only query doesn't need change tracking
- **Primary key lookup**: Query by `Id` uses primary key index (fastest possible query)
- **No joins needed**: Feed entity contains all required data via JSON columns (Selectors)

### Caching Strategy
- **Response caching** with FusionCache:
  - Cache key: `feed:detail:{feedId}`
  - TTL: 5 minutes (300 seconds)
  - Invalidate on feed update/delete
- **ETag support**: Client can use `If-None-Match` for conditional requests (future enhancement)

### Potential Bottlenecks
- **None expected**: Single-row primary key lookup is extremely fast
- **JSON deserialization**: EF Core handles automatically with compiled expressions

## 9. Implementation Steps

### 1. Create Contract Types
**File**: `src/RSSVibe.Contracts/Feeds/FeedDetailResponse.cs`
- Define `FeedDetailResponse` positional record with all properties

**File**: `src/RSSVibe.Contracts/Feeds/CacheHeadersDto.cs`
- Define `CacheHeadersDto` positional record

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/GetFeedDetailQuery.cs`
- Define `GetFeedDetailQuery` positional record

**File**: `src/RSSVibe.Services/Feeds/GetFeedDetailResult.cs`
- Define `GetFeedDetailResult` record with `FeedDetail` nested record

**File**: `src/RSSVibe.Services/Feeds/FeedDetailError.cs`
- Define `FeedDetailError` enum

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (update)
- Add method signature: `Task<GetFeedDetailResult> GetFeedDetailAsync(GetFeedDetailQuery query, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (update)
- Implement `GetFeedDetailAsync` method:
  1. Query feeds with AsNoTracking()
  2. Filter by feedId
  3. Check if feed exists
  4. Validate ownership
  5. Map to result

### 3. Implement Validation
No custom validation needed (UUID format validated by ASP.NET Core).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/GetFeedDetailEndpoint.cs`
- Create `GetFeedDetailEndpoint` static class
- Implement `MapGetFeedDetailEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<Ok<FeedDetailResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound>>
      HandleAsync(Guid feedId, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Create query
- Call service
- Compute cache headers and ETag
- Map result to response with TypedResults
- Add OpenAPI metadata

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (update)
- Add call to `group.MapGetFeedDetailEndpoint();` in `MapFeedsGroup()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/GetFeedDetailEndpointTests.cs`

Test scenarios:
1. `GetFeedDetailEndpoint_WithValidFeedId_ShouldReturnFeedDetails`
   - Arrange: Create feed for authenticated user
   - Act: GET /feeds/{feedId}
   - Assert: 200 OK, all feed details present including selectors

2. `GetFeedDetailEndpoint_WithNonExistentFeedId_ShouldReturnNotFound`
   - Arrange: Random UUID
   - Act: GET /feeds/{randomId}
   - Assert: 404 Not Found

3. `GetFeedDetailEndpoint_WithFeedBelongingToDifferentUser_ShouldReturnForbidden`
   - Arrange: Create feed for user2, authenticate as user1
   - Act: GET /feeds/{user2FeedId}
   - Assert: 403 Forbidden

4. `GetFeedDetailEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
   - Arrange: Create feed
   - Act: GET /feeds/{feedId} without auth token
   - Assert: 401 Unauthorized

5. `GetFeedDetailEndpoint_ShouldIncludeCacheHeaders`
   - Arrange: Create feed
   - Act: GET /feeds/{feedId}
   - Assert: 200 OK, CacheHeaders populated with expected values

6. `GetFeedDetailEndpoint_ShouldIncludePublicRssUrl`
   - Arrange: Create feed
   - Act: GET /feeds/{feedId}
   - Assert: 200 OK, RssUrl = "/feed/{userId}/{feedId}"

7. `GetFeedDetailEndpoint_ShouldReturnJsonSelectors`
   - Arrange: Create feed with custom selectors
   - Act: GET /feeds/{feedId}
   - Assert: 200 OK, selectors match expected configuration

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/GetFeedDetailEndpoint.cs`
- Add XML documentation comments to handler method
- Add `.WithOpenApi()` configuration with detailed description and response examples
