# API Endpoint Implementation Plan: POST /api/v1/feeds

## 1. Endpoint Overview

This endpoint approves a completed `FeedAnalysis` and creates an active `Feed` configuration. It copies selectors, schedule metadata, and user preferences from the analysis into a persistent feed record. The endpoint validates that the analysis is complete, has no blocking preflight flags, and that the normalized source URL is unique for the authenticated user. Upon successful creation, it schedules the feed for periodic parsing by setting `NextParseAfter` and returns the feed details including the public RSS URL.

## 2. Request Details

- **HTTP Method**: POST
- **URL Structure**: `/api/v1/feeds`
- **Parameters**:
  - **Path**: None
  - **Query**: None
  - **Body**:
    ```json
    {
      "analysisId": "uuid",
      "title": "Example News",
      "description": "Latest updates from Example",
      "language": "en",
      "updateInterval": {
        "unit": "hour",
        "value": 1
      },
      "ttlMinutes": 60,
      "selectorsOverride": null
    }
    ```
- **Authentication**: Required (JWT Bearer Token). User must own the referenced `FeedAnalysis`.

## 3. Used Types

### Request Contracts

**Location**: `RSSVibe.Contracts/Feeds/CreateFeedRequest.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to create a feed from a completed analysis.
/// </summary>
public sealed record CreateFeedRequest(
    Guid AnalysisId,
    string Title,
    string? Description,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    FeedSelectorsDto? SelectorsOverride
)
{
    public sealed class Validator : AbstractValidator<CreateFeedRequest>
    {
        public Validator()
        {
            RuleFor(x => x.AnalysisId)
                .NotEmpty()
                .WithMessage("Analysis ID is required.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.")
                .MaximumLength(200)
                .WithMessage("Title must not exceed 200 characters.");

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
                .NotNull()
                .WithMessage("Update interval is required.")
                .SetValidator(new UpdateIntervalDto.Validator());

            RuleFor(x => x.TtlMinutes)
                .GreaterThanOrEqualTo((short)15)
                .WithMessage("TTL must be at least 15 minutes.");

            RuleFor(x => x.SelectorsOverride)
                .SetValidator(new FeedSelectorsDto.Validator()!)
                .When(x => x.SelectorsOverride is not null);
        }
    }
}
```

**Location**: `RSSVibe.Contracts/Feeds/UpdateIntervalDto.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Represents the update interval for a feed.
/// </summary>
public sealed record UpdateIntervalDto(
    string Unit,
    short Value
)
{
    public sealed class Validator : AbstractValidator<UpdateIntervalDto>
    {
        public Validator()
        {
            RuleFor(x => x.Unit)
                .NotEmpty()
                .WithMessage("Update interval unit is required.")
                .Must(u => u is "hour" or "day" or "week")
                .WithMessage("Update interval unit must be 'hour', 'day', or 'week'.");

            RuleFor(x => x.Value)
                .GreaterThanOrEqualTo((short)1)
                .WithMessage("Update interval value must be at least 1.");

            // Ensure computed interval is at least 1 hour
            RuleFor(x => x)
                .Must(interval =>
                {
                    var hours = interval.Unit switch
                    {
                        "hour" => interval.Value,
                        "day" => interval.Value * 24,
                        "week" => interval.Value * 24 * 7,
                        _ => 0
                    };
                    return hours >= 1;
                })
                .WithMessage("Computed update interval must be at least 1 hour.");
        }
    }
}
```

**Location**: `RSSVibe.Contracts/Feeds/FeedSelectorsDto.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// CSS selectors for extracting feed items from a webpage.
/// </summary>
public sealed record FeedSelectorsDto(
    string List,
    string Item,
    string Title,
    string Link,
    string? Published,
    string? Summary
)
{
    public sealed class Validator : AbstractValidator<FeedSelectorsDto>
    {
        public Validator()
        {
            RuleFor(x => x.List)
                .NotEmpty()
                .WithMessage("List selector is required.")
                .MaximumLength(500)
                .WithMessage("List selector must not exceed 500 characters.");

            RuleFor(x => x.Item)
                .NotEmpty()
                .WithMessage("Item selector is required.")
                .MaximumLength(500)
                .WithMessage("Item selector must not exceed 500 characters.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title selector is required.")
                .MaximumLength(500)
                .WithMessage("Title selector must not exceed 500 characters.");

            RuleFor(x => x.Link)
                .NotEmpty()
                .WithMessage("Link selector is required.")
                .MaximumLength(500)
                .WithMessage("Link selector must not exceed 500 characters.");

            RuleFor(x => x.Published)
                .MaximumLength(500)
                .WithMessage("Published selector must not exceed 500 characters.")
                .When(x => x.Published is not null);

            RuleFor(x => x.Summary)
                .MaximumLength(500)
                .WithMessage("Summary selector must not exceed 500 characters.")
                .When(x => x.Summary is not null);
        }
    }
}
```

### Response Contracts

**Location**: `RSSVibe.Contracts/Feeds/CreateFeedResponse.cs`

```csharp
namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Response after successfully creating a feed.
/// </summary>
public sealed record CreateFeedResponse(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string Title,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string RssUrl
);
```

### Service Layer Types

**Location**: `RSSVibe.Services/Feeds/CreateFeedCommand.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Command to create a feed from a completed analysis.
/// </summary>
public sealed record CreateFeedCommand(
    Guid AnalysisId,
    Guid UserId,
    string Title,
    string? Description,
    string? Language,
    string UpdateIntervalUnit,
    short UpdateIntervalValue,
    short TtlMinutes,
    FeedSelectors? SelectorsOverride
);
```

**Location**: `RSSVibe.Services/Feeds/CreateFeedResult.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Result of creating a feed.
/// </summary>
public sealed record CreateFeedResult
{
    public bool Success { get; init; }
    public Guid FeedId { get; init; }
    public Guid UserId { get; init; }
    public string? SourceUrl { get; init; }
    public string? Title { get; init; }
    public string? Language { get; init; }
    public string? UpdateIntervalUnit { get; init; }
    public short UpdateIntervalValue { get; init; }
    public short TtlMinutes { get; init; }
    public DateTime? LastParsedAt { get; init; }
    public DateTime? NextParseAfter { get; init; }
    public FeedCreationError? Error { get; init; }
}
```

**Location**: `RSSVibe.Services/Feeds/FeedCreationError.cs`

```csharp
namespace RSSVibe.Services.Feeds;

/// <summary>
/// Errors that can occur when creating a feed.
/// </summary>
public enum FeedCreationError
{
    AnalysisNotFound,
    AnalysisNotOwned,
    AnalysisNotCompleted,
    AnalysisHasBlockingPreflightFlags,
    DuplicateNormalizedSourceUrl,
    SchedulerUnavailable
}
```

## 4. Response Details

### Success (201 Created)

**Headers**:
- `Location`: `/api/v1/feeds/{feedId}`

**Body**:
```json
{
  "feedId": "uuid",
  "userId": "uuid",
  "sourceUrl": "https://example.com/news",
  "title": "Example News",
  "language": "en",
  "updateInterval": {
    "unit": "hour",
    "value": 1
  },
  "ttlMinutes": 60,
  "lastParsedAt": null,
  "nextParseAfter": "2024-05-01T13:00:00Z",
  "rssUrl": "/feed/{userId}/{feedId}"
}
```

### Errors

| Status Code | Scenario | Response Type |
|-------------|----------|---------------|
| 400 | Validation failures (title too long, invalid schedule, etc.) | `ValidationProblem` |
| 401 | Unauthenticated | `UnauthorizedHttpResult` |
| 403 | Analysis belongs to different user | `ForbidHttpResult` |
| 404 | Analysis not found | `NotFound` |
| 409 | Duplicate normalized source URL | `Conflict` |
| 422 | Analysis not completed or has blocking preflight flags | `UnprocessableEntity` |
| 503 | Scheduler unavailable | `ServiceUnavailable` |

## 5. Data Flow

1. **Endpoint receives request** with `CreateFeedRequest` in body and extracts authenticated user ID from JWT claims (`ClaimsPrincipal`).

2. **FluentValidation automatically validates** the request using `CreateFeedRequest.Validator` before the handler executes (via SharpGrip.FluentValidation.AutoValidation.Endpoints). Returns `400 Bad Request` with validation problem details if validation fails.

3. **Handler maps DTO to command** and calls `IFeedService.CreateFeedAsync(CreateFeedCommand)`:
   - Extracts user ID from `HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)`
   - Maps `UpdateIntervalDto` to separate `UpdateIntervalUnit` and `UpdateIntervalValue` properties
   - Maps `FeedSelectorsDto?` to `FeedSelectors?` model (if override provided)

4. **Service validates analysis ownership and status**:
   - Query `FeedAnalyses` table for analysis with matching `AnalysisId`
   - Verify `analysis.UserId == command.UserId` (return `FeedCreationError.AnalysisNotOwned` if not)
   - Verify `analysis.AnalysisStatus == FeedAnalysisStatus.Completed` (return `FeedCreationError.AnalysisNotCompleted` if not)
   - Check `analysis.PreflightChecks` for blocking flags (`RequiresJavascript`, `RequiresAuthentication`, `Paywalled`) (return `FeedCreationError.AnalysisHasBlockingPreflightFlags` if any set)

5. **Service checks for duplicate normalized source URL**:
   - Extract `NormalizedSourceUrl` from analysis
   - Query `Feeds` table for existing feed with `(UserId = command.UserId, NormalizedSourceUrl = analysis.NormalizedSourceUrl)`
   - Return `FeedCreationError.DuplicateNormalizedSourceUrl` if found

6. **Service creates Feed entity** using database transaction with execution strategy:
   - Generate new UUIDv7 for feed ID: `Guid.CreateVersion7()`
   - Copy `SourceUrl`, `NormalizedSourceUrl` from analysis
   - Set `Title`, `Description`, `Language` from command
   - Set `Selectors` from `command.SelectorsOverride ?? analysis.Selectors`
   - Set `UpdateIntervalUnit`, `UpdateIntervalValue`, `TtlMinutes` from command
   - Calculate `NextParseAfter` based on current time + update interval
   - Set `AnalysisId = command.AnalysisId`
   - Add feed to `DbContext.Feeds`

7. **Service updates FeedAnalysis record**:
   - Set `analysis.ApprovedFeedId = feedId`
   - Mark analysis as updated (UpdateTimestampsInterceptor handles `UpdatedAt`)

8. **Service commits transaction** and returns `CreateFeedResult` with `Success = true`.

9. **Handler maps result to response**:
   - If `result.Error` is not null, map to appropriate HTTP status code and return error response
   - Otherwise, map to `CreateFeedResponse` and return `TypedResults.Created` with `Location` header

## 6. Security Considerations

### Authentication
- **Required**: JWT Bearer Token in `Authorization` header
- Extract user ID from `ClaimTypes.NameIdentifier` claim

### Authorization
- **Resource ownership**: Verify `analysis.UserId == authenticated user ID` before creating feed
- No role-based requirements (all authenticated users can create feeds)

### Input Validation
- **FluentValidation** prevents:
  - Missing required fields
  - Title/description exceeding length limits
  - Invalid language codes
  - Invalid update interval configurations
  - Malformed selector schemas
- **Database constraints** enforce:
  - Unique (UserId, NormalizedSourceUrl) via index
  - Check constraints on UpdateIntervalValue (≥1) and TtlMinutes (≥15)

### Data Integrity
- Use **execution strategy with transaction** to ensure atomicity
- Validate analysis status and preflight checks before feed creation
- Prevent duplicate feeds with same source URL per user

### Rate Limiting
- Not explicitly required for this endpoint, but consider adding rate limiting per user to prevent abuse

### Additional Security
- HTTPS required for all API communication
- Sanitize error messages to avoid leaking sensitive information (e.g., don't expose internal database errors)
- Log authorization failures with user ID and attempted analysis ID for security monitoring

## 7. Error Handling

| Scenario | Status Code | Response |
|----------|-------------|----------|
| Missing/invalid required fields | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Title": ["Title is required."] } }` |
| Title exceeds 200 characters | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "Title": ["Title must not exceed 200 characters."] } }` |
| Invalid update interval unit | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "UpdateInterval.Unit": ["Update interval unit must be 'hour', 'day', or 'week'."] } }` |
| Computed interval less than 1 hour | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "UpdateInterval": ["Computed update interval must be at least 1 hour."] } }` |
| Invalid selectors schema | 400 | `{ "type": "validation_error", "title": "Validation failed", "errors": { "SelectorsOverride.List": ["List selector is required."] } }` |
| Unauthenticated request | 401 | `TypedResults.Unauthorized()` |
| Analysis belongs to different user | 403 | `TypedResults.Forbid()` |
| Analysis not found | 404 | `TypedResults.NotFound()` or `{ "type": "analysis_not_found", "title": "Analysis not found", "detail": "The specified analysis does not exist." }` |
| Duplicate normalized source URL | 409 | `{ "type": "duplicate_resource", "title": "Feed already exists", "detail": "A feed with the same source URL already exists for this user." }` |
| Analysis not completed | 422 | `{ "type": "analysis_not_completed", "title": "Analysis not completed", "detail": "The analysis must be in 'completed' status before creating a feed." }` |
| Analysis has blocking preflight flags | 422 | `{ "type": "preflight_failed", "title": "Preflight checks failed", "detail": "The analysis contains blocking preflight flags (RequiresJavascript, RequiresAuthentication, or Paywalled)." }` |
| Database/scheduler unavailable | 503 | `{ "type": "service_unavailable", "title": "Service unavailable", "detail": "Unable to create feed at this time. Please try again later." }` |

## 8. Performance Considerations

### Database Query Optimization
- **Indexed queries**:
  - Lookup analysis by ID (primary key index)
  - Check duplicate normalized URL with composite index on `(UserId, NormalizedSourceUrl)`
- **Eager loading**: Load analysis with `Include(a => a.User)` if user info needed (not required for this endpoint)
- **AsNoTracking()**: Not applicable for write operations

### Caching Strategy
- **No caching** for write operations
- Consider invalidating cached feed list for user after successful creation (if caching is implemented for `GET /api/v1/feeds`)

### Transaction Management
- **Use execution strategy** (PostgreSQL requirement):
  ```csharp
  var strategy = dbContext.Database.CreateExecutionStrategy();
  return await strategy.ExecuteAsync(async () =>
  {
      await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
      try
      {
          // Create feed and update analysis
          await dbContext.SaveChangesAsync(cancellationToken);
          await transaction.CommitAsync(cancellationToken);
          return successResult;
      }
      catch
      {
          await transaction.RollbackAsync(cancellationToken);
          throw;
      }
  });
  ```

### Potential Bottlenecks
- **Analysis validation queries**: Mitigated by primary key lookup (fast)
- **Duplicate source URL check**: Mitigated by composite index
- **Transaction overhead**: Necessary for data consistency, minimal impact
- **NextParseAfter calculation**: Simple arithmetic, no performance concern

## 9. Implementation Steps

### 1. Create Contract Types
**File**: `src/RSSVibe.Contracts/Feeds/CreateFeedRequest.cs`
- Define `CreateFeedRequest` positional record with all required properties
- Create nested `Validator` class extending `AbstractValidator<CreateFeedRequest>`
- Implement validation rules for all fields

**File**: `src/RSSVibe.Contracts/Feeds/UpdateIntervalDto.cs`
- Define `UpdateIntervalDto` positional record with `Unit` and `Value`
- Create nested `Validator` class with unit/value validation and computed interval check

**File**: `src/RSSVibe.Contracts/Feeds/FeedSelectorsDto.cs`
- Define `FeedSelectorsDto` positional record with all selector fields
- Create nested `Validator` class with length validation

**File**: `src/RSSVibe.Contracts/Feeds/CreateFeedResponse.cs`
- Define `CreateFeedResponse` positional record with all response properties

### 2. Create Service Layer
**File**: `src/RSSVibe.Services/Feeds/CreateFeedCommand.cs`
- Define `CreateFeedCommand` positional record with all command parameters

**File**: `src/RSSVibe.Services/Feeds/CreateFeedResult.cs`
- Define `CreateFeedResult` record with success flag and all response data

**File**: `src/RSSVibe.Services/Feeds/FeedCreationError.cs`
- Define `FeedCreationError` enum with all possible error cases

**File**: `src/RSSVibe.Services/Feeds/IFeedService.cs` (create or update)
- Add method signature: `Task<CreateFeedResult> CreateFeedAsync(CreateFeedCommand command, CancellationToken ct);`

**File**: `src/RSSVibe.Services/Feeds/FeedService.cs` (create or update)
- Implement `CreateFeedAsync` method:
  1. Query analysis by ID with user ID verification
  2. Validate analysis status and preflight checks
  3. Check for duplicate normalized source URL
  4. Use execution strategy to wrap transaction
  5. Create Feed entity with UUIDv7 ID
  6. Update FeedAnalysis.ApprovedFeedId
  7. Save changes and commit transaction
  8. Return result with success or error

### 3. Implement Validation
Validators are already defined as nested classes in contract types (step 1).

### 4. Create Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/CreateFeedEndpoint.cs`
- Create `CreateFeedEndpoint` static class
- Implement `MapCreateFeedEndpoint(this RouteGroupBuilder group)` extension method
- Define handler method:
  ```csharp
  private static async Task<Results<Created<CreateFeedResponse>, ValidationProblem, UnauthorizedHttpResult, ForbidHttpResult, NotFound, Conflict, UnprocessableEntity, ServiceUnavailable>> 
      HandleAsync(CreateFeedRequest request, ClaimsPrincipal user, IFeedService feedService, CancellationToken ct)
  ```
- Extract user ID from claims
- Map request to command
- Call service
- Map result to appropriate TypedResults response
- Add OpenAPI metadata with `.WithName("CreateFeed")`, `.WithTags("Feeds")`, `.WithOpenApi()`

### 5. Register Endpoint
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/FeedsGroup.cs` (create if not exists)
- Create `FeedsGroup` static class
- Implement `MapFeedsGroup(this IEndpointRouteBuilder endpoints)` extension method
- Create `/feeds` group with auth requirement:
  ```csharp
  var group = endpoints.MapGroup("/feeds")
      .WithTags("Feeds")
      .RequireAuthorization();
  ```
- Call `group.MapCreateFeedEndpoint();`

**File**: `src/RSSVibe.ApiService/Endpoints/ApiGroup.cs` (update)
- Add call to `group.MapFeedsGroup();` in `MapApiV1()` method

### 6. Add Tests
**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Feeds/CreateFeedEndpointTests.cs`

Test scenarios:
1. `CreateFeedEndpoint_WithValidRequest_ShouldReturnCreatedWithFeedData`
   - Arrange: Create completed analysis, valid request
   - Act: POST to endpoint with authenticated client
   - Assert: 201 Created, Location header, response data matches request

2. `CreateFeedEndpoint_WithMissingTitle_ShouldReturnValidationError`
   - Arrange: Request with empty title
   - Act: POST to endpoint
   - Assert: 400 Bad Request with validation errors

3. `CreateFeedEndpoint_WithTitleExceeding200Characters_ShouldReturnValidationError`
   - Arrange: Request with 201-character title
   - Act: POST to endpoint
   - Assert: 400 Bad Request

4. `CreateFeedEndpoint_WithInvalidUpdateInterval_ShouldReturnValidationError`
   - Arrange: Request with invalid unit or value < 1
   - Act: POST to endpoint
   - Assert: 400 Bad Request

5. `CreateFeedEndpoint_WithComputedIntervalLessThan1Hour_ShouldReturnValidationError`
   - Arrange: Request with valid unit/value but computed < 1 hour (shouldn't be possible with current constraints, but test edge case)
   - Act: POST to endpoint
   - Assert: 400 Bad Request

6. `CreateFeedEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized`
   - Arrange: Valid request, no auth token
   - Act: POST to endpoint with unauthenticated client
   - Assert: 401 Unauthorized

7. `CreateFeedEndpoint_WithAnalysisBelongingToDifferentUser_ShouldReturnForbidden`
   - Arrange: Create analysis for different user, valid request
   - Act: POST to endpoint with authenticated client
   - Assert: 403 Forbidden

8. `CreateFeedEndpoint_WithNonExistentAnalysisId_ShouldReturnNotFound`
   - Arrange: Request with random UUID
   - Act: POST to endpoint
   - Assert: 404 Not Found

9. `CreateFeedEndpoint_WithDuplicateSourceUrl_ShouldReturnConflict`
   - Arrange: Create analysis and feed, attempt to create second feed with same source URL
   - Act: POST to endpoint
   - Assert: 409 Conflict

10. `CreateFeedEndpoint_WithPendingAnalysis_ShouldReturnUnprocessableEntity`
    - Arrange: Create analysis with status = pending
    - Act: POST to endpoint
    - Assert: 422 Unprocessable Entity

11. `CreateFeedEndpoint_WithBlockingPreflightFlags_ShouldReturnUnprocessableEntity`
    - Arrange: Create completed analysis with RequiresJavascript flag
    - Act: POST to endpoint
    - Assert: 422 Unprocessable Entity

12. `CreateFeedEndpoint_WithSelectorsOverride_ShouldUseOverrideInFeed`
    - Arrange: Valid request with selectorsOverride
    - Act: POST to endpoint
    - Assert: 201 Created, verify feed has override selectors (query database)

13. `CreateFeedEndpoint_SuccessfulCreation_ShouldSetNextParseAfter`
    - Arrange: Valid request
    - Act: POST to endpoint
    - Assert: 201 Created, verify NextParseAfter is set correctly in database

14. `CreateFeedEndpoint_SuccessfulCreation_ShouldLinkAnalysisToFeed`
    - Arrange: Valid request
    - Act: POST to endpoint
    - Assert: 201 Created, verify analysis.ApprovedFeedId is set in database

### 7. Update Documentation
**File**: `src/RSSVibe.ApiService/Endpoints/Feeds/CreateFeedEndpoint.cs`
- Add XML documentation comments to handler method
- Add `.WithOpenApi()` configuration with detailed description, request/response examples, and status codes
