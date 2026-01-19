# API Endpoint Implementation Plan: GET /api/v1/feed-analyses

## 1. Endpoint Overview

This endpoint lists feed analyses for the authenticated user with support for filtering, sorting, pagination, and search capabilities. It serves as the primary interface for dashboards and moderation workflows to track AI-powered analysis requests.

**Key Characteristics**:
- Returns paginated list of feed analyses scoped to the authenticated user
- Supports filtering by analysis status (pending, inProgress, completed, failed, superseded)
- Allows sorting by creation or update timestamp (ascending/descending)
- Enables search by partial URL match using normalized URLs
- Uses read-only queries with `AsNoTracking()` for optimal performance
- Leverages database indexes for efficient filtering and sorting
- Returns `TypedResults` for type-safe responses and automatic OpenAPI documentation

## 2. Request Details

- **HTTP Method**: GET
- **URL Structure**: `/api/v1/feed-analyses`
- **Authentication**: Required (JWT Bearer token)
- **Content-Type**: `application/json`

### Query Parameters

**Optional**:
- `status` (FeedAnalysisStatus?): Filter by analysis status
  - Valid values: `Pending`, `InProgress`, `Completed`, `Failed`, `Superseded`
  - Default: null (no filter)
- `sort` (string?): Sort order for results
  - Valid values: `createdAt:asc`, `createdAt:desc`, `updatedAt:asc`, `updatedAt:desc`
  - Default: `createdAt:desc`
- `skip` (int): Number of records to skip for pagination
  - Valid range: >= 0
  - Default: 0
- `take` (int): Number of records to return per page
  - Valid range: 1-50
  - Default: 20
- `search` (string?): Partial URL match against normalized URL
  - Valid: null or non-empty string
  - Default: null (no search filter)

### Request Example
```http
GET /api/v1/feed-analyses?status=Completed&sort=createdAt:desc&skip=0&take=20&search=example.com
Authorization: Bearer {jwt-token}
```

## 3. Response Details

### Success Response (200 OK)

**Body**:
```json
{
  "items": [
    {
      "analysisId": "01936d4a-8f2e-7890-abcd-123456789012",
      "targetUrl": "https://example.com/news",
      "status": "completed",
      "warnings": ["Possible paywall", "RequiresJavascript"],
      "analysisStartedAt": "2024-05-01T12:01:00Z",
      "analysisCompletedAt": "2024-05-01T12:02:10Z"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "totalCount": 45,
    "hasMore": true
  }
}
```

### Error Responses

**401 Unauthorized**: Missing or invalid JWT token
- Body: Standard ASP.NET Core auth challenge

**422 Unprocessable Entity**: Validation errors
- Body: FluentValidation error response
- Examples:
  - Invalid sort format
  - Take value out of range (not 1-50)
  - Skip value negative
  - Search is empty string (must be null or non-empty)

## 4. Used Types

### Request/Response Contracts (Already Exist)

**Location**: `RSSVibe.Contracts/FeedAnalyses/`

```csharp
// Request with validation
public sealed record ListFeedAnalysesRequest(
    FeedAnalysisStatus? Status,
    string? Sort,
    int Skip,
    int Take,
    string? Search
)
{
    public sealed class Validator : AbstractValidator<ListFeedAnalysesRequest>
    {
        // Validation rules already implemented
    }
}

// Response
public sealed record ListFeedAnalysesResponse(
    FeedAnalysisListItemDto[] Items,
    PagingDto Paging
);

// DTO for list items
public sealed record FeedAnalysisListItemDto(
    Guid AnalysisId,
    string TargetUrl,
    string Status,
    string[] Warnings,
    DateTimeOffset? AnalysisStartedAt,
    DateTimeOffset? AnalysisCompletedAt
);

// Paging metadata
public sealed record PagingDto(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);
```

### Service Layer Types (To Be Created)

**Location**: `RSSVibe.Services/FeedAnalyses/`

```csharp
// Command model
public sealed record ListFeedAnalysesCommand(
    Guid UserId,
    FeedAnalysisStatus? Status,
    string? Sort,
    int Skip,
    int Take,
    string? Search
);

// Result type
public sealed record ListFeedAnalysesResult
{
    public required FeedAnalysisListItem[] Items { get; init; }
    public required PagingMetadata Paging { get; init; }
    public bool Success { get; init; }
    public FeedAnalysisError? Error { get; init; }
}

// Service-layer DTO (maps to contract DTO)
public sealed record FeedAnalysisListItem(
    Guid AnalysisId,
    string TargetUrl,
    FeedAnalysisStatus Status,
    string[] Warnings,
    DateTimeOffset? AnalysisStartedAt,
    DateTimeOffset? AnalysisCompletedAt
);

// Paging metadata
public sealed record PagingMetadata(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);

// Error enum (already exists, may need extension)
public enum FeedAnalysisError
{
    None,
    DatabaseUnavailable,
    InvalidSortParameter
}
```

### Entities (Already Exist)

**Location**: `RSSVibe.Data/Entities/FeedAnalysis.cs`

```csharp
public sealed class FeedAnalysis : IAuditableEntity
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string TargetUrl { get; set; }
    public required string NormalizedUrl { get; set; }
    public FeedAnalysisStatus AnalysisStatus { get; set; }
    public string[] Warnings { get; set; } = [];
    public DateTimeOffset? AnalysisStartedAt { get; set; }
    public DateTimeOffset? AnalysisCompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    // ... other properties
}

public enum FeedAnalysisStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Superseded
}
```

## 5. Data Flow

### Query Construction

The service layer builds an EF Core query with the following logic:

1. **Base Query**: Start with all `FeedAnalysis` entities for the authenticated user
   ```csharp
   var query = dbContext.FeedAnalyses
       .AsNoTracking()
       .Where(fa => fa.UserId == command.UserId);
   ```

2. **Status Filter** (if provided):
   ```csharp
   if (command.Status.HasValue)
   {
       query = query.Where(fa => fa.AnalysisStatus == command.Status.Value);
   }
   ```

3. **Search Filter** (if provided):
   ```csharp
   if (!string.IsNullOrWhiteSpace(command.Search))
   {
       query = query.Where(fa => fa.NormalizedUrl.Contains(command.Search));
   }
   ```

4. **Sorting**:
   ```csharp
   query = command.Sort?.ToLowerInvariant() switch
   {
       "createdat:asc" => query.OrderBy(fa => fa.CreatedAt),
       "createdat:desc" or null => query.OrderByDescending(fa => fa.CreatedAt),
       "updatedat:asc" => query.OrderBy(fa => fa.UpdatedAt),
       "updatedat:desc" => query.OrderByDescending(fa => fa.UpdatedAt),
       _ => query.OrderByDescending(fa => fa.CreatedAt)
   };
   ```

5. **Total Count** (before pagination):
   ```csharp
   var totalCount = await query.CountAsync(cancellationToken);
   ```

6. **Pagination + Projection**:
   ```csharp
   var items = await query
       .Skip(command.Skip)
       .Take(command.Take)
       .Select(fa => new FeedAnalysisListItem(
           fa.Id,
           fa.TargetUrl,
           fa.AnalysisStatus,
           fa.Warnings,
           fa.AnalysisStartedAt,
           fa.AnalysisCompletedAt
       ))
       .ToArrayAsync(cancellationToken);
   ```

7. **Paging Metadata**:
   ```csharp
   var paging = new PagingMetadata(
       command.Skip,
       command.Take,
       totalCount,
       HasMore: command.Skip + items.Length < totalCount
   );
   ```

### Data Mapping

**Service → Endpoint**:
```csharp
var response = new ListFeedAnalysesResponse(
    Items: result.Items.Select(i => new FeedAnalysisListItemDto(
        i.AnalysisId,
        i.TargetUrl,
        i.Status.ToString().ToLowerInvariant(),
        i.Warnings,
        i.AnalysisStartedAt,
        i.AnalysisCompletedAt
    )).ToArray(),
    Paging: new PagingDto(
        result.Paging.Skip,
        result.Paging.Take,
        result.Paging.TotalCount,
        result.Paging.HasMore
    )
);
```

## 6. Security

### Authentication & Authorization

- **MUST** require JWT Bearer authentication for all requests
- **MUST** extract `UserId` from JWT claims (`ClaimTypes.NameIdentifier`)
- **MUST** filter all queries by `UserId` to ensure user isolation
- **MUST NOT** allow users to access other users' feed analyses

### Input Validation

- Validation handled by `ListFeedAnalysesRequest.Validator` via FluentValidation auto-validation
- Validation runs automatically before handler execution
- Invalid requests return 422 Unprocessable Entity

### Rate Limiting

- SHOULD implement rate limiting at API gateway or middleware level
- Recommended: 100 requests per minute per user for read operations

## 7. Error Handling

### Expected Error Scenarios

**401 Unauthorized**:
- Missing JWT token
- Invalid or expired token
- User not found in claims

**422 Unprocessable Entity**:
- Invalid `status` enum value
- Invalid `sort` format
- `skip` < 0
- `take` < 1 or > 50
- `search` is empty string (must be null or non-empty)

**503 Service Unavailable**:
- Database connection failure
- DbContext unavailable

### Error Response Format

FluentValidation errors (422):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "Sort": ["Sort must be one of: createdAt:asc, createdAt:desc, updatedAt:asc, updatedAt:desc"]
  }
}
```

## 8. Performance Considerations

### Database Optimization

**Indexes Required** (verify in `FeedAnalysisConfiguration.cs`):
1. `(UserId, NormalizedUrl)` - For user filtering and search
2. `(UserId, AnalysisStatus, CreatedAt DESC)` - For filtered lists with default sort
3. `(UserId, UpdatedAt DESC)` - For updatedAt sorting

**Query Optimization**:
- Use `AsNoTracking()` for read-only queries (30-50% faster)
- Project directly to DTOs to avoid loading unnecessary columns
- Execute `CountAsync()` before pagination to calculate total

**Expected Query Pattern**:
```sql
SELECT 
    fa."Id", 
    fa."TargetUrl", 
    fa."AnalysisStatus", 
    fa."Warnings",
    fa."AnalysisStartedAt",
    fa."AnalysisCompletedAt"
FROM "FeedAnalyses" fa
WHERE fa."UserId" = @userId
  AND (@status IS NULL OR fa."AnalysisStatus" = @status)
  AND (@search IS NULL OR fa."NormalizedUrl" LIKE '%' || @search || '%')
ORDER BY fa."CreatedAt" DESC
OFFSET @skip ROWS
FETCH NEXT @take ROWS ONLY;
```

### Caching Strategy

- **NOT recommended** for this endpoint (data changes frequently)
- If caching is needed, use short TTL (5-10 seconds) with user-specific keys

## 9. Implementation Steps

### Step 1: Extend Service Layer

**File**: `RSSVibe.Services/FeedAnalyses/IFeedAnalysisService.cs`

Add method signature:
```csharp
Task<ListFeedAnalysesResult> ListFeedAnalysesAsync(
    ListFeedAnalysesCommand command,
    CancellationToken cancellationToken = default);
```

**File**: `RSSVibe.Services/FeedAnalyses/FeedAnalysisService.cs`

Implement method:
```csharp
public async Task<ListFeedAnalysesResult> ListFeedAnalysesAsync(
    ListFeedAnalysesCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Build query with filters
        var query = _dbContext.FeedAnalyses
            .AsNoTracking()
            .Where(fa => fa.UserId == command.UserId);

        if (command.Status.HasValue)
            query = query.Where(fa => fa.AnalysisStatus == command.Status.Value);

        if (!string.IsNullOrWhiteSpace(command.Search))
            query = query.Where(fa => fa.NormalizedUrl.Contains(command.Search));

        // Apply sorting
        query = command.Sort?.ToLowerInvariant() switch
        {
            "createdat:asc" => query.OrderBy(fa => fa.CreatedAt),
            "updatedat:asc" => query.OrderBy(fa => fa.UpdatedAt),
            "updatedat:desc" => query.OrderByDescending(fa => fa.UpdatedAt),
            _ => query.OrderByDescending(fa => fa.CreatedAt)
        };

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Get paginated items with projection
        var items = await query
            .Skip(command.Skip)
            .Take(command.Take)
            .Select(fa => new FeedAnalysisListItem(
                fa.Id,
                fa.TargetUrl,
                fa.AnalysisStatus,
                fa.Warnings,
                fa.AnalysisStartedAt,
                fa.AnalysisCompletedAt
            ))
            .ToArrayAsync(cancellationToken);

        var paging = new PagingMetadata(
            command.Skip,
            command.Take,
            totalCount,
            HasMore: command.Skip + items.Length < totalCount
        );

        return new ListFeedAnalysesResult
        {
            Items = items,
            Paging = paging,
            Success = true,
            Error = null
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to list feed analyses for user {UserId}", command.UserId);
        return new ListFeedAnalysesResult
        {
            Items = [],
            Paging = new PagingMetadata(0, 0, 0, false),
            Success = false,
            Error = FeedAnalysisError.DatabaseUnavailable
        };
    }
}
```

**Files to Create** (in `RSSVibe.Services/FeedAnalyses/`):
- `ListFeedAnalysesCommand.cs`
- `ListFeedAnalysesResult.cs`

### Step 2: Create Endpoint

**File**: `RSSVibe.ApiService/Endpoints/FeedAnalyses/ListFeedAnalysesEndpoint.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class ListFeedAnalysesEndpoint
{
    public static RouteGroupBuilder MapListFeedAnalysesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("ListFeedAnalyses")
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "List feed analyses for current user";
                op.Description = "Returns paginated list of feed analyses with filtering, sorting, and search capabilities.";
                return op;
            });

        return group;
    }

    private static async Task<Results<Ok<ListFeedAnalysesResponse>, ProblemHttpResult, UnauthorizedHttpResult>>
        HandleAsync(
            ClaimsPrincipal user,
            IFeedAnalysisService feedAnalysisService,
            [AsParameters] ListFeedAnalysesRequest request,
            CancellationToken cancellationToken)
    {
        // Extract user ID from claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        // Build service command
        var command = new ListFeedAnalysesCommand(
            userId,
            request.Status,
            request.Sort,
            request.Skip,
            request.Take,
            request.Search
        );

        // Execute service
        var result = await feedAnalysisService.ListFeedAnalysesAsync(command, cancellationToken);

        if (!result.Success)
        {
            return TypedResults.Problem(
                title: "Failed to retrieve feed analyses",
                detail: result.Error?.ToString(),
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        // Map to response
        var response = new ListFeedAnalysesResponse(
            Items: result.Items.Select(i => new FeedAnalysisListItemDto(
                i.AnalysisId,
                i.TargetUrl,
                i.Status.ToString().ToLowerInvariant(),
                i.Warnings,
                i.AnalysisStartedAt,
                i.AnalysisCompletedAt
            )).ToArray(),
            Paging: new PagingDto(
                result.Paging.Skip,
                result.Paging.Take,
                result.Paging.TotalCount,
                result.Paging.HasMore
            )
        );

        return TypedResults.Ok(response);
    }
}
```

**File**: `RSSVibe.ApiService/Endpoints/FeedAnalyses/FeedAnalysesGroup.cs`

Add registration:
```csharp
public static IEndpointRouteBuilder MapFeedAnalysesGroup(this IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/feed-analyses")
        .WithTags("FeedAnalyses");

    group.MapCreateFeedAnalysisEndpoint(); // existing
    group.MapListFeedAnalysesEndpoint();   // new

    return endpoints;
}
```

### Step 3: Write Integration Tests

**File**: `Tests/RSSVibe.ApiService.Tests/Endpoints/FeedAnalyses/ListFeedAnalysesEndpointTests.cs`

Test scenarios to implement:

1. **Success Cases**:
    - `ListFeedAnalyses_WithValidRequest_ShouldReturnPagedResults`
    - `ListFeedAnalyses_WithStatusFilter_ShouldReturnFilteredResults`
    - `ListFeedAnalyses_WithStatusFilter_Pending_ShouldReturnOnlyPendingAnalyses`
    - `ListFeedAnalyses_WithStatusFilter_InProgress_ShouldReturnOnlyInProgressAnalyses`
    - `ListFeedAnalyses_WithStatusFilter_Completed_ShouldReturnOnlyCompletedAnalyses`
    - `ListFeedAnalyses_WithStatusFilter_Failed_ShouldReturnOnlyFailedAnalyses`
    - `ListFeedAnalyses_WithStatusFilter_Superseded_ShouldReturnOnlySupersededAnalyses`
    - `ListFeedAnalyses_WithSearchTerm_ShouldReturnMatchingResults`
    - `ListFeedAnalyses_WithSortParameter_ShouldReturnSortedResults`
    - `ListFeedAnalyses_WithPagination_ShouldReturnCorrectPage`

2. **Validation Failures**:
    - `ListFeedAnalyses_WithInvalidSort_ShouldReturnValidationError`
    - `ListFeedAnalyses_WithNegativeSkip_ShouldReturnValidationError`
    - `ListFeedAnalyses_WithTakeOutOfRange_ShouldReturnValidationError`
    - `ListFeedAnalyses_WithEmptySearch_ShouldReturnValidationError`
    - `ListFeedAnalyses_WithInvalidStatus_ShouldReturnValidationError`

3. **Authentication**:
    - `ListFeedAnalyses_WithoutAuthentication_ShouldReturnUnauthorized`
    - `ListFeedAnalyses_WithValidToken_ShouldReturnOnlyUserAnalyses`

Example tests:

**Test with various status values including InProgress**:
```csharp
[Test]
public async Task ListFeedAnalyses_WithValidRequest_ShouldReturnPagedResults()
{
    // Arrange - Create authenticated client
    var client = CreateAuthenticatedClient();

    // Create test data with mixed statuses
    await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
    
    var userId = Guid.Parse(TestApplication.TestUserId);
    var analyses = new[]
    {
        new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TargetUrl = "https://test1.example.com/feed",
            NormalizedUrl = "https://test1.example.com/feed",
            AnalysisStatus = FeedAnalysisStatus.Pending,
            Warnings = [],
            PreflightDetails = new FeedPreflightDetails(),
            Selectors = new FeedSelectors(),
            CreatedAt = DateTimeOffset.UtcNow
        },
        new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TargetUrl = "https://test2.example.com/feed",
            NormalizedUrl = "https://test2.example.com/feed",
            AnalysisStatus = FeedAnalysisStatus.InProgress,
            Warnings = [],
            PreflightDetails = new FeedPreflightDetails(),
            Selectors = new FeedSelectors(),
            AnalysisStartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        },
        new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TargetUrl = "https://test3.example.com/feed",
            NormalizedUrl = "https://test3.example.com/feed",
            AnalysisStatus = FeedAnalysisStatus.Completed,
            Warnings = ["Test warning"],
            PreflightDetails = new FeedPreflightDetails(),
            Selectors = new FeedSelectors(),
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            AnalysisCompletedAt = DateTimeOffset.UtcNow
        }
    };
    
    dbContext.FeedAnalyses.AddRange(analyses);
    await dbContext.SaveChangesAsync();

    // Act
    var response = await client.GetAsync("/api/v1/feed-analyses?skip=0&take=20");

    // Assert
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    
    var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();
    await Assert.That(data).IsNotNull();
    await Assert.That(data.Items).HasCount().GreaterThanOrEqualTo(3);
    await Assert.That(data.Paging.TotalCount).IsGreaterThanOrEqualTo(3);
}

[Test]
public async Task ListFeedAnalyses_WithInProgressStatusFilter_ShouldReturnOnlyInProgressAnalyses()
{
    // Arrange
    var client = CreateAuthenticatedClient();
    await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
    
    var userId = Guid.Parse(TestApplication.TestUserId);
    
    // Create analyses with different statuses
    var inProgressAnalysis = new FeedAnalysis
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        TargetUrl = "https://inprogress.example.com/feed",
        NormalizedUrl = "https://inprogress.example.com/feed",
        AnalysisStatus = FeedAnalysisStatus.InProgress,
        Warnings = [],
        PreflightDetails = new FeedPreflightDetails(),
        Selectors = new FeedSelectors(),
        AnalysisStartedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow
    };
    
    var completedAnalysis = new FeedAnalysis
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        TargetUrl = "https://completed.example.com/feed",
        NormalizedUrl = "https://completed.example.com/feed",
        AnalysisStatus = FeedAnalysisStatus.Completed,
        Warnings = [],
        PreflightDetails = new FeedPreflightDetails(),
        Selectors = new FeedSelectors(),
        AnalysisCompletedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow
    };
    
    dbContext.FeedAnalyses.AddRange(inProgressAnalysis, completedAnalysis);
    await dbContext.SaveChangesAsync();

    // Act
    var response = await client.GetAsync("/api/v1/feed-analyses?status=InProgress");

    // Assert
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();
    await Assert.That(data.Items).HasCount().GreaterThanOrEqualTo(1);
    await Assert.That(data.Items.All(i => i.Status == "inprogress")).IsTrue();
}
```

### Step 4: Verify Database Indexes

**File**: `RSSVibe.Data/Configurations/FeedAnalysisConfiguration.cs`

Ensure these indexes exist:
```csharp
builder.HasIndex(x => new { x.UserId, x.NormalizedUrl });
builder.HasIndex(x => new { x.UserId, x.AnalysisStatus, x.CreatedAt });
builder.HasIndex(x => new { x.UserId, x.UpdatedAt });
```

If missing, create a migration:
```bash
cd src/RSSVibe.Data
bash add_migration.sh AddFeedAnalysisListIndexes
```

## 10. Testing Checklist

- [ ] Endpoint returns 200 OK with valid request
- [ ] Endpoint returns correct pagination metadata (skip, take, totalCount, hasMore)
- [ ] Status filter works correctly (pending, inProgress, completed, failed, superseded)
- [ ] Search filter matches partial URLs correctly
- [ ] Sorting works for all valid combinations (createdAt/updatedAt, asc/desc)
- [ ] Pagination returns correct page of results
- [ ] Invalid sort parameter returns 422 validation error
- [ ] Negative skip returns 422 validation error
- [ ] Take < 1 or > 50 returns 422 validation error
- [ ] Empty search string returns 422 validation error
- [ ] Missing authentication returns 401 Unauthorized
- [ ] User can only see their own feed analyses (user isolation verified)
- [ ] Database indexes are used (verify with EXPLAIN ANALYZE in PostgreSQL)

## 11. OpenAPI Documentation

The endpoint will automatically generate OpenAPI documentation with:

**Tags**: `FeedAnalyses`

**Summary**: List feed analyses for current user

**Description**: Returns paginated list of feed analyses with filtering, sorting, and search capabilities.

**Security**: `Bearer` (JWT)

**Parameters**:
- `status` (query, optional): Filter by analysis status
- `sort` (query, optional): Sort order
- `skip` (query, required): Pagination offset
- `take` (query, required): Page size
- `search` (query, optional): URL search term

**Responses**:
- `200`: Success with `ListFeedAnalysesResponse`
- `401`: Unauthorized
- `422`: Validation error

## 12. Future Enhancements

**Potential improvements** (not in initial implementation):

1. **Advanced Search**: Support for multiple search criteria (URL, warnings, date ranges)
2. **Response Caching**: Short-lived cache with ETag support for frequently accessed pages
3. **Cursor-based Pagination**: More efficient for large datasets
4. **Aggregated Statistics**: Include counts by status in response metadata
5. **Export**: CSV/JSON export for all user analyses
6. **Bulk Operations**: Mark multiple analyses as superseded, delete multiple analyses
