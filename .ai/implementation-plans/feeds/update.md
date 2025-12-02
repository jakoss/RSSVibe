# API Endpoint Implementation Plan: PATCH /api/v1/feeds/{feedId}

## 1. Endpoint Overview

This endpoint updates mutable fields of an existing feed configuration. Users can modify the title, description, language, selectors, update schedule, and TTL. The endpoint validates that the authenticated user owns the feed and that all updated values comply with business rules. Changes to the schedule trigger recalculation of `NextParseAfter`. At least one field must be provided for update.

## 2. Request Details

- **HTTP Method**: PATCH
- **URL Structure**: `/api/v1/feeds/{feedId}`
- **Parameters**:
  - **Path**:
    - `feedId` (uuid, required): Unique identifier of the feed to update
  - **Query**: None
  - **Body** (all fields optional, but at least one required):
    ```json
    {
      "title": "Example News - Updated",
      "description": "Curated updates",
      "language": "en",
      "updateInterval": {
        "unit": "hour",
        "value": 2
      },
      "ttlMinutes": 90,
      "selectorsOverride": {
        "list": ".articles",
        "item": ".article",
        "title": ".title",
        "link": ".title a",
        "published": ".date"
      }
    }
    ```
- **Authentication**: Required (JWT Bearer Token). User must own the feed.

## 3. Used Types

### Request Contracts

**Location**: `RSSVibe.Contracts/Feeds/UpdateFeedRequest.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to update a feed configuration.
/// </summary>
public sealed record UpdateFeedRequest(
    string? Title = null,
    string? Description = null,
    string? Language = null,
    UpdateIntervalDto? UpdateInterval = null,
    short? TtlMinutes = null,
    FeedSelectorsDto? SelectorsOverride = null
)
{
    public sealed class Validator : AbstractValidator<UpdateFeedRequest>
    {
        public Validator()
        {
            // At least one field must be provided
            RuleFor(x => x)
                .Must(r => r.Title is not null 
                         || r.Description is not null 
                         || r.Language is not null 
                         || r.UpdateInterval is not null 
                         || r.TtlMinutes is not null 
                         || r.SelectorsOverride is not null)
                .WithMessage("At least one field must be provided for update.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title cannot be empty.")
                .MaximumLength(200)
                .WithMessage("Title must not exceed 200 characters.")
                .When(x => x.Title is not null);

            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithMessage("Description must not exceed 2000 characters.")
                .When(x => x.Description is not null);

            RuleFor(x => x.Language)
                .MaximumLength(16)
                .WithMessage("Language code must not exceed 16 characters.")
                .Matches(@"^[a-z]{2,3}(-[A-Z]{2})?$")
                .WithMessage("Language must be a valid ISO 639-1/2 code (e.g., 'en', 'en-US').")
                .When(x => x.Language is not null);

            RuleFor(x => x.UpdateInterval)
                .SetValidator(new UpdateIntervalDto.Validator()!)
                .When(x => x.UpdateInterval is not null);

            RuleFor(x => x.TtlMinutes)
                .GreaterThanOrEqualTo((short)15)
                .WithMessage("TTL must be at least 15 minutes.")
                .When(x => x.TtlMinutes is not null);

            RuleFor(x => x.SelectorsOverride)
                .SetValidator(new FeedSelectorsDto.Validator()!)
                .When(x => x.SelectorsOverride is not null);
        }
    }
}
```

### Response Contracts

**Location**: `RSSVibe.Contracts/Feeds/UpdateFeedResponse.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response after successfully updating a feed.
/// </summary>
public sealed record UpdateFeedResponse(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string Title,
    string? Description,
    string? Language,
    FeedSelectorsDto Selectors,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    DateTime? NextParseAfter,
    DateTime UpdatedAt
);
```

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/UpdateFeedCommand.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Command to update a feed.
/// </summary>
public sealed record UpdateFeedCommand(
    Guid FeedId,
    Guid UserId,
    string? Title,
    string? Description,
    string? Language,
    string? UpdateIntervalUnit,
    short? UpdateIntervalValue,
    short? TtlMinutes,
    FeedSelectors? SelectorsOverride
);
```

**Location**: `RSSVibe.Services/Feeds/UpdateFeedResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of updating a feed.
/// </summary>
public sealed record UpdateFeedResult
{
    public bool Success { get; init; }
    public FeedDetail? Feed { get; init; }
    public FeedUpdateError? Error { get; init; }
}
```

**Location**: `RSSVibe.Services/Feeds/FeedUpdateError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when updating a feed.
/// </summary>
public enum FeedUpdateError
{
    FeedNotFound,
    FeedNotOwned,
    SchedulerUnavailable
}
```

## 4. Response Details

### Success (200 OK)

**Body**:
```json
{
  "feedId": "uuid",
  "userId": "uuid",
  "sourceUrl": "https://example.com/news",
  "title": "Example News - Updated",
  "description": "Curated updates",
  "language": "en",
  "selectors": {
    "list": ".articles",
    "item": ".article",
    "title": ".title",
    "link": ".title a",
    "published": ".date"
  },
  "updateInterval": {
    "unit": "hour",
    "value": 2
  },
  "ttlMinutes": 90,
  "nextParseAfter": "2024-05-01T15:00:00Z",
  "updatedAt": "2024-05-01T13:05:00Z"
}
```

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 400 | Validation failures (no fields provided, title too long, invalid schedule, etc.) | `ValidationProblem` |
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 403 | Feed belongs to different user | `ForbidHttpResult` |
| 404 | Feed not found | `NotFound` |
| 422 | Invalid selector schema or schedule | `UnprocessableEntity` |
| 503 | Scheduler unavailable | `ServiceUnavailable` |

## 5. Data Flow

1. **Endpoint receives request** with `feedId` path parameter and `UpdateFeedRequest` body, extracts authenticated user ID from JWT claims.

2. **FluentValidation automatically validates** the request using `UpdateFeedRequest.Validator` before handler executes. Returns `400 Bad Request` if validation fails (no fields provided, title too long, etc.).

3. **Handler maps request to command**:
   - Extract user ID from `ClaimsPrincipal`
   - Map `UpdateIntervalDto?` to separate `UpdateIntervalUnit` and `UpdateIntervalValue` (if provided)
   - Map `FeedSelectorsDto?` to `FeedSelectors?` model (if provided)
   - Create `UpdateFeedCommand` with feedId, userId, and all update fields

4. **Service queries and validates feed**:
   - Query `Feeds` table for feed with `Id == command.FeedId`
   - Return `FeedUpdateError.FeedNotFound` if not found
   - Verify `feed.UserId == command.UserId` (return `FeedUpdateError.FeedNotOwned` if not)

5. **Service applies updates using execution strategy with transaction**:
   - If `Title` provided, update `feed.Title`
   - If `Description` provided, update `feed.Description`
   - If `Language` provided, update `feed.Language`
   - If `UpdateInterval` provided:
     - Update `feed.UpdateIntervalUnit` and `feed.UpdateIntervalValue`
     - Recalculate `feed.NextParseAfter` based on current time + new interval
   - If `TtlMinutes` provided, update `feed.TtlMinutes`
   - If `SelectorsOverride` provided, update `feed.Selectors`

6. **Service saves changes**:
   - `UpdateTimestampsInterceptor` automatically updates `feed.UpdatedAt`
   - Commit transaction
   - Return successful result with updated feed data

7. **Handler maps result to response**:
   - If `result.Error` is not null, map to appropriate HTTP status code
   - Map `FeedDetail` to `UpdateFeedResponse`
   - Return `TypedResults.Ok`

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Resource ownership**: Verify `feed.UserId == authenticated user ID`
- Return `403 Forbidden` if user doesn't own the feed

### Input Validation
- **FluentValidation** prevents:
  - Empty update requests (at least one field required)
  - Title/description exceeding length limits
  - Invalid language codes
  - Invalid update interval configurations
  - Malformed selector schemas
  - TTL values below minimum (15 minutes)
- **Database constraints** enforce:
  - Check constraints on UpdateIntervalValue (≥1) and TtlMinutes (≥15)

### Data Integrity
- Use **execution strategy with transaction** to ensure atomicity
- Recalculate `NextParseAfter` when schedule changes to maintain consistency

### Additional Security
- HTTPS required for all API communication
- Log update attempts for security monitoring (especially 403 responses)
- Sanitize error messages to avoid leaking sensitive information

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| No fields provided | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "": ["At least one field must be provided for update."] } }` |
| Title exceeds 200 characters | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Title": ["Title must not exceed 200 characters."] } }` |
| Invalid update interval | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "UpdateInterval.Unit": ["Update interval unit must be 'hour', 'day', or 'week'."] } }` |
| TTL below minimum | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "TtlMinutes": ["TTL must be at least 15 minutes."] } }` |
| Invalid selectors schema | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "SelectorsOverride.List": ["List selector is required."] } }` |
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Feed belongs to different user | 403 | `TypedResults.Forbid()` |
| Feed not found | 404 | `TypedResults.NotFound()` |
| Invalid selector schema (business rule) | 422 | `{ "type": "invalid_selectors", "title": "Invalid selector schema", "detail": "The provided selectors do not match the required schema." }` |
| Scheduler unavailable | 503 | `{ "type": "service_unavailable", "title": "Service unavailable", "detail": "Unable to update feed schedule at this time. Please try again later." }` |

## 8. Performance Considerations

### Database Query Optimization
- **Primary key lookup**: Query by `Id` uses primary key index
- **Transaction with execution strategy**: Required for PostgreSQL, ensures atomicity

### Caching Strategy
- **Invalidate cached feed detail** after successful update:
  - Cache key: `feed:detail:{feedId}`
- **Invalidate cached feed list** for user:
  - Cache key: `feeds:list:{userId}:*` (wildcard invalidation)

### Potential Bottlenecks
- **NextParseAfter recalculation**: Simple arithmetic, no performance concern
- **Transaction overhead**: Necessary for data consistency, minimal impact

## 9. Implementation Steps

### 1. Create Contract Types
**File**: `src/RSSVibe.Contracts/Feeds/UpdateFeedRequest.cs`
- Define `UpdateFeedRequest` positional record with all optional properties
- Create nested `Validator` class with validation rules including "at least one field" check

**File**: `src/RSSVibe.Contracts/Feeds/UpdateFeedResponse.cs`
- Define `UpdateFeedResponse` positional record

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/UpdateFeedCommand.cs`
- Define `UpdateFeedCommand` positional record with optional update fields

**File**: `src/RSSVibe.Services/Feeds/UpdateFeedResult.cs`
- Define `UpdateFeedResult` record

**File**: `src/RSSVibe.Services/Feeds/FeedUpdateError.cs`
- Define `FeedUpdateError` enum

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (update)
- Add method signature: `Task<UpdateFeedResult> UpdateFeedAsync(UpdateFeedCommand command, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (update)
- Implement `UpdateFeedAsync` method:
  1. Query feed by ID
  2. Validate ownership
  3. Use execution strategy with transaction
  4. Apply updates conditionally (only provided fields)
  5. Recalculate NextParseAfter if schedule changed
  6. Save changes and commit transaction
  7. Return result

### 3. Implement Validation
Validator is already defined as nested class in `UpdateFeedRequest` (step 1).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/UpdateFeedEndpoint.cs`
- Create `UpdateFeedEndpoint` static class
- Implement `MapUpdateFeedEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<Ok<UpdateFeedResponse>, ValidationProblem, UnauthorizedHttpResult, ForbidHttpResult, NotFound, UnprocessableEntity, ServiceUnavailable>>
      HandleAsync(Guid feedId, UpdateFeedRequest request, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Map request to command
- Call service
- Map result to response with TypedResults
- Add OpenAPI metadata

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (update)
- Add call to `group.MapUpdateFeedEndpoint();` in `MapFeedsGroup()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/UpdateFeedEndpointTests.cs`

Test scenarios:
1. `UpdateFeedEndpoint_WithValidTitleUpdate_ShouldUpdateFeed`
2. `UpdateFeedEndpoint_WithMultipleFieldUpdates_ShouldUpdateAllFields`
3. `UpdateFeedEndpoint_WithScheduleUpdate_ShouldRecalculateNextParseAfter`
4. `UpdateFeedEndpoint_WithSelectorsOverride_ShouldUpdateSelectors`
5. `UpdateFeedEndpoint_WithEmptyRequest_ShouldReturnValidationError`
6. `UpdateFeedEndpoint_WithTitleExceeding200Characters_ShouldReturnValidationError`
7. `UpdateFeedEndpoint_WithInvalidLanguageCode_ShouldReturnValidationError`
8. `UpdateFeedEndpoint_WithTtlBelowMinimum_ShouldReturnValidationError`
9. `UpdateFeedEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
10. `UpdateFeedEndpoint_WithFeedBelongingToDifferentUser_ShouldReturnForbidden`
11. `UpdateFeedEndpoint_WithNonExistentFeedId_ShouldReturnNotFound`
12. `UpdateFeedEndpoint_SuccessfulUpdate_ShouldUpdateTimestamp`

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/UpdateFeedEndpoint.cs`
- Add XML documentation comments
- Add `.WithOpenApi()` configuration with detailed description
