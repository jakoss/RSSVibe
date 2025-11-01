# API Endpoint Implementation Plan: GET /api/v1/auth/profile

## 1. Endpoint Overview

This endpoint retrieves the authenticated user's profile information including security posture metadata. It provides essential user details such as email, display name, assigned roles, and whether a password change is required. This endpoint is referenced in the Location header of POST /api/v1/auth/register to allow clients to immediately fetch the newly created user's profile.

**Key Characteristics**:
- Authenticated endpoint requiring valid JWT token
- Read-only operation (GET)
- Returns data only for the authenticated user (no user enumeration)
- Includes security metadata (mustChangePassword flag)
- No query parameters or request body

## 2. Request Details

- **HTTP Method**: GET
- **URL Structure**: `/api/v1/auth/profile`
- **Authentication**: Required (JWT Bearer Token)
- **Authorization Header**: `Authorization: Bearer {jwt_token}`

### Parameters

**Required**:
- JWT token in Authorization header (user ID extracted from claims)

**Optional**:
- None

### Request Body
- Not applicable (GET request)

### User Context
- User ID extracted from `ClaimsPrincipal` (populated by JWT authentication middleware)
- Standard claim type: `ClaimTypes.NameIdentifier` or `sub` claim

## 3. Used Types

### Response DTO (Existing)

**Location**: `/src/RSSVibe.Contracts/Auth/ProfileResponse.cs`

```csharp
public sealed record ProfileResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool MustChangePassword,
    DateTimeOffset CreatedAt
);
```

### Command Model (New)

**Location**: `/src/RSSVibe.Services/Auth/GetUserProfileCommand.cs`

```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for retrieving user profile by user ID.
/// </summary>
public sealed record GetUserProfileCommand(
    Guid UserId
);
```

### Result Model (New)

**Location**: `/src/RSSVibe.Services/Auth/GetUserProfileResult.cs`

```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of user profile retrieval operation.
/// </summary>
public sealed record GetUserProfileResult
{
    public bool Success { get; init; }
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string[] Roles { get; init; } = [];
    public bool MustChangePassword { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public ProfileError? Error { get; init; }
}
```

### Error Enum (New)

**Location**: `/src/RSSVibe.Services/Auth/ProfileError.cs`

```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Errors that can occur during user profile retrieval.
/// </summary>
public enum ProfileError
{
    /// <summary>
    /// User not found in the identity store (data inconsistency).
    /// </summary>
    UserNotFound,

    /// <summary>
    /// Identity store (database) is unavailable or unreachable.
    /// </summary>
    IdentityStoreUnavailable
}
```

## 4. Response Details

### Success Response (200 OK)

```json
{
  "userId": "018c5e4a-7b6c-7890-abcd-ef1234567890",
  "email": "user@example.com",
  "displayName": "Jane Doe",
  "roles": ["User"],
  "mustChangePassword": false,
  "createdAt": "2024-05-01T12:01:00Z"
}
```

**Content-Type**: `application/json`

### Error Responses

#### 401 Unauthorized
**Trigger**: Missing, invalid, or expired JWT token

Handled by ASP.NET Core authentication middleware before endpoint execution.

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

#### 503 Service Unavailable
**Trigger**: Database connection failure or user not found (data inconsistency)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service temporarily unavailable",
  "detail": "Unable to retrieve user profile. Please try again later.",
  "status": 503
}
```

## 5. Data Flow

### Request Flow

1. **HTTP Request** arrives with JWT token in `Authorization: Bearer {token}` header
2. **Authentication Middleware** validates JWT signature, expiration, and claims
3. **Authorization Middleware** verifies user is authenticated (returns 401 if not)
4. **Endpoint Handler** receives authenticated `HttpContext` with populated `ClaimsPrincipal`
5. **User ID Extraction** from `ClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)`
6. **Command Creation**: `GetUserProfileCommand` with extracted user ID
7. **Service Call**: `IAuthService.GetUserProfileAsync(command, cancellationToken)`

### Service Layer Flow

8. **User Lookup**: `UserManager<ApplicationUser>.FindByIdAsync(userId)`
9. **Existence Check**: Return error if user not found
10. **Role Retrieval**: `UserManager<ApplicationUser>.GetRolesAsync(user)`
11. **Data Mapping**: Map `ApplicationUser` entity to `GetUserProfileResult`
12. **Result Return**: Return success result with user data

### Response Flow

13. **Result Handling**: Endpoint checks `result.Success`
14. **Error Mapping**: Map `ProfileError` to appropriate HTTP status code
15. **Response Mapping**: Create `ProfileResponse` from successful result
16. **HTTP Response**: Return `TypedResults.Ok(response)` or error response

### Database Interactions

- **AspNetUsers Table**: Single query via `UserManager.FindByIdAsync()`
  - Retrieves: Id, Email, DisplayName, MustChangePassword, CreatedAt (converted from IdentityUser timestamp)
- **AspNetRoles + AspNetUserRoles Tables**: Query via `UserManager.GetRolesAsync()`
  - Retrieves: Array of role names assigned to user
  - Typical result: `["User"]` for standard users

### External Dependencies

- **ASP.NET Identity UserManager**: User and role retrieval
- **Entity Framework Core**: Database access through UserManager
- **PostgreSQL**: Data persistence layer

## 6. Security Considerations

### Authentication Requirements

- **JWT Validation**: Endpoint MUST require `RequireAuthorization()` on route group or endpoint
- **Token Verification**: Authentication middleware validates:
  - Token signature using configured JWT secret
  - Token expiration (exp claim)
  - Issuer and audience claims (if configured)
  - Token not revoked (basic validation - refresh token revocation doesn't affect access tokens until expiry)

### Authorization

- **User Context**: Only authenticated user can access their own profile
- **No Impersonation**: User ID is extracted from validated JWT claims (cannot be spoofed)
- **No User Enumeration**: Endpoint does not accept user ID as parameter (prevents enumeration attacks)

### Data Protection

- **PII Handling**: Email, display name are sensitive - only returned to authenticated owner
- **Role Information**: User roles exposed only to the user themselves (not a security risk)
- **Password Hash**: Never exposed in response (handled by ASP.NET Identity)
- **Security Metadata**: `mustChangePassword` flag helps enforce password rotation policies

### JWT Claims Security

- **Claim Extraction**: Use `ClaimTypes.NameIdentifier` or standard `sub` claim for user ID
- **GUID Validation**: Ensure extracted claim can be parsed as valid GUID
- **Claim Tampering**: JWT signature validation prevents claim tampering
- **Token Lifetime**: Access tokens should have short expiration (15-60 minutes)

### Logging Security

- **Email Anonymization**: Use existing `AnonymizeEmail()` pattern for log entries
- **No PII in Logs**: Avoid logging full user data in error scenarios
- **Structured Logging**: Use structured logging with sanitized parameters

### Potential Security Threats

1. **JWT Theft**: Mitigated by HTTPS, short token lifetime, refresh token rotation
2. **Token Replay**: Not applicable (endpoint is read-only, no state modification)
3. **Data Inconsistency Attack**: User in JWT but not in database - treated as 503 (service error)
4. **Timing Attacks**: Use constant-time operations for user lookups (Identity handles this)

## 7. Error Handling

### Error Scenarios and Status Codes

| Scenario | Error | Status Code | Handler |
|----------|-------|-------------|---------|
| Missing JWT token | N/A | 401 Unauthorized | Authentication middleware |
| Invalid JWT token | N/A | 401 Unauthorized | Authentication middleware |
| Expired JWT token | N/A | 401 Unauthorized | Authentication middleware |
| Invalid user ID in claims | N/A | 401 Unauthorized | GUID parsing in endpoint |
| User not found in database | ProfileError.UserNotFound | 503 Service Unavailable | Endpoint handler |
| Database connection failure | ProfileError.IdentityStoreUnavailable | 503 Service Unavailable | Service layer (DbException) |
| UserManager exception | ProfileError.IdentityStoreUnavailable | 503 Service Unavailable | Service layer (catch block) |
| Unexpected exception | N/A | 500 Internal Server Error | Exception middleware |

### Error Handling Implementation

**Endpoint Layer**:
```csharp
// Extract and validate user ID
if (!Guid.TryParse(userIdClaim, out var userId))
{
    return TypedResults.Problem(
        title: "Invalid user identity",
        detail: "User ID in token is invalid.",
        statusCode: StatusCodes.Status401Unauthorized);
}

// Handle service result
if (!result.Success)
{
    return result.Error switch
    {
        ProfileError.UserNotFound => TypedResults.Problem(
            title: "Service temporarily unavailable",
            detail: "Unable to retrieve user profile. Please try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        ProfileError.IdentityStoreUnavailable => TypedResults.Problem(
            title: "Service temporarily unavailable",
            detail: "Unable to retrieve user profile. Please try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        _ => TypedResults.Problem(
            title: "Profile retrieval failed",
            detail: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError)
    };
}
```

**Service Layer**:
```csharp
try
{
    var user = await userManager.FindByIdAsync(command.UserId.ToString());
    if (user is null)
    {
        logger.LogWarning(
            "User profile requested but user not found in database: {UserId}",
            command.UserId);

        return new GetUserProfileResult
        {
            Success = false,
            Error = ProfileError.UserNotFound
        };
    }

    // Retrieve user data and roles...
}
catch (DbException ex)
{
    logger.LogError(
        ex,
        "Database error while retrieving user profile for user: {UserId}",
        command.UserId);

    return new GetUserProfileResult
    {
        Success = false,
        Error = ProfileError.IdentityStoreUnavailable
    };
}
catch (Exception ex)
{
    logger.LogError(
        ex,
        "Unexpected error while retrieving user profile for user: {UserId}",
        command.UserId);

    return new GetUserProfileResult
    {
        Success = false,
        Error = ProfileError.IdentityStoreUnavailable
    };
}
```

### Logging Strategy

**Success Scenario**:
```csharp
logger.LogInformation(
    "User profile retrieved successfully for user: {UserId}",
    command.UserId);
```

**Error Scenarios**:
- User not found: Warning level (potential JWT/DB inconsistency)
- Database errors: Error level with exception details
- Unexpected errors: Error level with full exception stack

## 8. Performance Considerations

### Current Implementation

- **Database Queries**: 2 queries per request
  - Query 1: Find user by ID (UserManager.FindByIdAsync)
  - Query 2: Get user roles (UserManager.GetRolesAsync)
- **Query Complexity**: Simple primary key lookup and join query
- **Expected Latency**: 5-20ms for database operations
- **No N+1 Queries**: Fixed 2-query pattern regardless of data size

### Potential Bottlenecks

1. **Database Connection Pool**: High traffic may exhaust connection pool
   - Mitigation: Ensure proper connection pooling configuration
   - Monitor: Connection pool metrics in production

2. **Role Query**: Separate query for roles (join between AspNetUserRoles and AspNetRoles)
   - Mitigation: Consider eager loading if UserManager supports it
   - Alternative: Single query with explicit EF Core Include if UserManager allows

3. **Repeated Profile Fetches**: Frontend may call this endpoint frequently
   - Example: On every page load for user context
   - Impact: Unnecessary database load for infrequently changing data

### Optimization Opportunities

**Phase 1: Current Implementation**
- No caching (keep it simple for MVP)
- Direct UserManager calls
- Simple error handling

**Phase 2: Caching Strategy (Future)**
```csharp
// Distributed caching with short TTL
var cacheKey = $"user:profile:{userId}";
var cachedProfile = await cache.GetAsync<ProfileResponse>(cacheKey);

if (cachedProfile is not null)
{
    return TypedResults.Ok(cachedProfile);
}

// Fetch from database
var result = await authService.GetUserProfileAsync(command, ct);

// Cache for 5-15 minutes
await cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(10));
```

**Cache Invalidation Events**:
- User updates display name
- User changes password (updates MustChangePassword flag)
- User roles modified
- User deleted

**Phase 3: Query Optimization (Future)**
- Single query with explicit Include for roles
- Projected DTO from EF query (avoid loading full entity)
- Compiled queries for frequently executed patterns

### Scalability

- **Stateless Endpoint**: Scales horizontally without session affinity
- **Database Read**: Read replicas can be used for profile queries
- **Caching Layer**: Redis/FusionCache can handle millions of cached profiles
- **JWT Validation**: No database hit for authentication (stateless tokens)

## 9. Implementation Steps

### Step 1: Define Service Contract Types

**File**: `/src/RSSVibe.Services/Auth/GetUserProfileCommand.cs`
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for retrieving user profile by user ID.
/// </summary>
public sealed record GetUserProfileCommand(
    Guid UserId
);
```

**File**: `/src/RSSVibe.Services/Auth/ProfileError.cs`
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Errors that can occur during user profile retrieval.
/// </summary>
public enum ProfileError
{
    UserNotFound,
    IdentityStoreUnavailable
}
```

**File**: `/src/RSSVibe.Services/Auth/GetUserProfileResult.cs`
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of user profile retrieval operation.
/// </summary>
public sealed record GetUserProfileResult
{
    public bool Success { get; init; }
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string[] Roles { get; init; } = [];
    public bool MustChangePassword { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public ProfileError? Error { get; init; }
}
```

### Step 2: Update IAuthService Interface

**File**: `/src/RSSVibe.Services/Auth/IAuthService.cs`

Add method signature:
```csharp
/// <summary>
/// Retrieves user profile information including roles and security metadata.
/// </summary>
/// <param name="command">Command containing user ID to retrieve profile for.</param>
/// <param name="cancellationToken">Cancellation token for the operation.</param>
/// <returns>Result containing user profile data on success or error information on failure.</returns>
Task<GetUserProfileResult> GetUserProfileAsync(
    GetUserProfileCommand command,
    CancellationToken cancellationToken = default);
```

### Step 3: Implement Service Method

**File**: `/src/RSSVibe.Services/Auth/AuthService.cs`

Add implementation:
```csharp
/// <inheritdoc />
public async Task<GetUserProfileResult> GetUserProfileAsync(
    GetUserProfileCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Find user by ID from JWT claims
        var user = await userManager.FindByIdAsync(command.UserId.ToString());

        if (user is null)
        {
            // User in JWT but not in database - data inconsistency
            logger.LogWarning(
                "User profile requested but user not found in database: {UserId}",
                command.UserId);

            return new GetUserProfileResult
            {
                Success = false,
                Error = ProfileError.UserNotFound
            };
        }

        // Retrieve user roles from Identity
        var roles = await userManager.GetRolesAsync(user);

        logger.LogInformation(
            "User profile retrieved successfully for user: {UserId}",
            command.UserId);

        // Map entity to result
        return new GetUserProfileResult
        {
            Success = true,
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = [.. roles],
            MustChangePassword = user.MustChangePassword,
            CreatedAt = new DateTimeOffset(user.CreatedAt ?? DateTime.UtcNow, TimeSpan.Zero)
        };
    }
    catch (DbException ex)
    {
        logger.LogError(
            ex,
            "Database error while retrieving user profile for user: {UserId}",
            command.UserId);

        return new GetUserProfileResult
        {
            Success = false,
            Error = ProfileError.IdentityStoreUnavailable
        };
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Unexpected error while retrieving user profile for user: {UserId}",
            command.UserId);

        return new GetUserProfileResult
        {
            Success = false,
            Error = ProfileError.IdentityStoreUnavailable
        };
    }
}
```

**Note**: Verify that `ApplicationUser` has a `CreatedAt` property. If not, you'll need to:
- Add `CreatedAt` property to `ApplicationUser` entity
- Create a migration to add the column
- Or use a workaround like checking `Id` timestamp (UUIDv7 contains timestamp)

**Alternative CreatedAt extraction from UUIDv7**:
```csharp
// Extract timestamp from UUIDv7 if no CreatedAt column exists
CreatedAt = ExtractTimestampFromUuidV7(user.Id)

private static DateTimeOffset ExtractTimestampFromUuidV7(Guid uuid)
{
    var bytes = uuid.ToByteArray();
    // UUIDv7 stores Unix timestamp in first 48 bits (big-endian)
    if (BitConverter.IsLittleEndian)
    {
        Array.Reverse(bytes, 0, 8);
    }
    var timestampMs = BitConverter.ToInt64(bytes, 0) >> 16;
    return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
}
```

### Step 4: Create Profile Endpoint

**File**: `/src/RSSVibe.ApiService/Endpoints/Auth/ProfileEndpoint.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for retrieving authenticated user's profile.
/// </summary>
public static class ProfileEndpoint
{
    /// <summary>
    /// Maps the profile endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapProfileEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", HandleAsync)
            .WithName("GetUserProfile")
            .RequireAuthorization() // IMPORTANT: Require authentication
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get current user profile";
                operation.Description = "Retrieves the authenticated user's profile including " +
                    "email, display name, roles, and security posture metadata. " +
                    "Requires valid JWT authentication token.";
                return operation;
            })
            .Produces<ProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<
        Ok<ProfileResponse>,
        ProblemHttpResult>>
        HandleAsync(
            ClaimsPrincipal user,
            IAuthService authService,
            ILogger<ProfileEndpoint> logger,
            CancellationToken cancellationToken)
    {
        // Extract user ID from JWT claims
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning(
                "Profile request with invalid user ID claim: {Claim}",
                userIdClaim ?? "[null]");

            return TypedResults.Problem(
                title: "Invalid user identity",
                detail: "User ID in token is invalid.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Create command and call service
        var command = new GetUserProfileCommand(userId);
        var result = await authService.GetUserProfileAsync(command, cancellationToken);

        // Handle errors
        if (!result.Success)
        {
            return result.Error switch
            {
                ProfileError.UserNotFound => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to retrieve user profile. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                ProfileError.IdentityStoreUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to retrieve user profile. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Profile retrieval failed",
                    detail: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // Success - map to response DTO
        var response = new ProfileResponse(
            result.UserId,
            result.Email!,
            result.DisplayName!,
            result.Roles,
            result.MustChangePassword,
            result.CreatedAt);

        return TypedResults.Ok(response);
    }
}
```

### Step 5: Register Endpoint in AuthGroup

**File**: `/src/RSSVibe.ApiService/Endpoints/Auth/AuthGroup.cs`

Add registration call:
```csharp
public static IEndpointRouteBuilder MapAuthGroup(
    this IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/auth")
        .WithTags("Auth");

    // Register all endpoints in the auth group
    group.MapRegisterEndpoint();
    group.MapLoginEndpoint();
    group.MapProfileEndpoint(); // ADD THIS LINE

    return endpoints;
}
```

### Step 6: Configure JWT Authentication (Verify Existing Setup)

**File**: `/src/RSSVibe.ApiService/Program.cs`

Verify that JWT authentication is configured. Add if missing:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Add after builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfiguration>();

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtConfig.Issuer,
        ValidAudience = jwtConfig.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtConfig.SecretKey)),
        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
    };
});

// Add authorization services
builder.Services.AddAuthorization();

// In app configuration (after app.UseExceptionHandler())
app.UseAuthentication(); // MUST come before UseAuthorization
app.UseAuthorization();
```

### Step 7: Handle ApplicationUser CreatedAt Field

**Option A: Add CreatedAt to ApplicationUser Entity** (Recommended)

**File**: `/src/RSSVibe.Data/Entities/ApplicationUser.cs`

```csharp
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Timestamp when user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // ADD THIS
}
```

Then create a migration:
```bash
cd src/RSSVibe.Data
bash add_migration.sh AddCreatedAtToApplicationUser
```

**Option B: Extract from UUIDv7** (If migration not feasible)

Use the UUIDv7 timestamp extraction helper method in AuthService.

### Step 8: Add Integration Tests

**File**: `/Tests/RSSVibe.ApiService.Tests/Endpoints/Auth/ProfileEndpointTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using RSSVibe.Contracts.Auth;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

public sealed class ProfileEndpointTests(TestApplication app) : IDisposable
{
    [Test]
    public async Task GetProfile_WithValidToken_Returns200WithUserData()
    {
        // Arrange
        var client = app.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.Email).IsEqualTo(TestApplication.TestUserEmail);
        await Assert.That(profile.DisplayName).IsNotNull();
        await Assert.That(profile.Roles).Contains("User");
        await Assert.That(profile.MustChangePassword).IsFalse();
    }

    [Test]
    public async Task GetProfile_WithoutToken_Returns401()
    {
        // Arrange
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetProfile_WithInvalidToken_Returns401()
    {
        // Arrange
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetProfile_WithExpiredToken_Returns401()
    {
        // Arrange - Create token with past expiration
        var expiredToken = app.CreateExpiredToken(TestApplication.TestUserId);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

**Helper Method in TestApplication** (if not exists):

```csharp
public string CreateExpiredToken(Guid userId)
{
    // Implementation to create JWT with past expiration
    // Use JwtTokenGenerator with custom expiration in the past
}
```

### Step 9: Manual Testing Checklist

1. Build the solution: `dotnet build -c Release -p:TreatWarningsAsErrors=true`
2. Run integration tests: `dotnet test`
3. Start the application: `dotnet run --project src/RSSVibe.ApiService`
4. Test with curl or Postman:

```bash
# Register a user
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecureP@ssw0rd123!",
    "displayName": "Test User"
  }'

# Login to get JWT token
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecureP@ssw0rd123!"
  }'

# Get profile (replace {token} with actual JWT)
curl -X GET http://localhost:5000/api/v1/auth/profile \
  -H "Authorization: Bearer {token}"

# Get profile without token (should return 401)
curl -X GET http://localhost:5000/api/v1/auth/profile
```

### Step 10: Documentation and Final Review

1. Verify OpenAPI documentation is generated correctly (check Scalar UI)
2. Confirm all error scenarios return appropriate status codes
3. Verify JWT authentication middleware is properly configured
4. Check logs for proper anonymization and structured logging
5. Review security: Ensure no PII exposure in error responses
6. Performance: Monitor database query count (should be exactly 2)
7. Update any relevant ADRs if architectural decisions were made

### Summary of Files to Create/Modify

**New Files**:
- `/src/RSSVibe.Services/Auth/GetUserProfileCommand.cs`
- `/src/RSSVibe.Services/Auth/ProfileError.cs`
- `/src/RSSVibe.Services/Auth/GetUserProfileResult.cs`
- `/src/RSSVibe.ApiService/Endpoints/Auth/ProfileEndpoint.cs`
- `/Tests/RSSVibe.ApiService.Tests/Endpoints/Auth/ProfileEndpointTests.cs`

**Modified Files**:
- `/src/RSSVibe.Services/Auth/IAuthService.cs` (add method signature)
- `/src/RSSVibe.Services/Auth/AuthService.cs` (implement method)
- `/src/RSSVibe.ApiService/Endpoints/Auth/AuthGroup.cs` (register endpoint)
- `/src/RSSVibe.Data/Entities/ApplicationUser.cs` (add CreatedAt if needed)
- `/src/RSSVibe.ApiService/Program.cs` (verify JWT auth configuration)

**Database Migration** (if adding CreatedAt):
- Create migration: `AddCreatedAtToApplicationUser`

**Existing Files** (no changes needed):
- `/src/RSSVibe.Contracts/Auth/ProfileResponse.cs` (already exists)
