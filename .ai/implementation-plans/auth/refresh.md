# API Endpoint Implementation Plan: POST /api/v1/auth/refresh

## 1. Endpoint Overview

The refresh token endpoint exchanges a valid refresh token for a new JWT access token and a new refresh token. This implements token rotation to enhance security and includes critical replay attack detection. When a token reuse attempt is detected (indicating potential token theft), the system revokes all tokens for the affected user as a security measure.

**Security Focus**: This endpoint is security-critical as it handles token rotation and must detect and prevent replay attacks where an attacker attempts to reuse a stolen refresh token.

**Token Rotation Strategy**: Each refresh operation generates a new refresh token and marks the old one as used, implementing a one-time-use pattern that enables replay detection.

## 2. Request Details

- **HTTP Method**: POST
- **URL Structure**: `/api/v1/auth/refresh`
- **Content-Type**: application/json

### Parameters

**Request Body** (RefreshTokenRequest):
```json
{
  "refreshToken": "base64-encoded-string"
}
```

**Required Fields**:
- `refreshToken` (string): The refresh token to exchange for new tokens
  - Must be non-empty
  - Validated by `RefreshTokenRequest.Validator` (already exists)
  - Expected format: Base64-encoded 64-byte random value (~88 characters)

**Authentication**: No authentication required (the refresh token itself authenticates the request)

## 3. Used Types

### Existing Types (No Changes Required)

**Request/Response DTOs** (in RSSVibe.Contracts/Auth/):
- `RefreshTokenRequest` - Request DTO with FluentValidation validator
- `RefreshTokenResponse` - Response DTO with access token, refresh token, expiration, and mustChangePassword flag

**Database Entities** (in RSSVibe.Data/Entities/):
- `RefreshToken` - Entity storing server-side refresh token state
  - Properties: Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt, IsUsed
  - Navigation: User (ApplicationUser)
- `ApplicationUser` - ASP.NET Identity user entity
  - Includes MustChangePassword flag

**Existing Services**:
- `IJwtTokenGenerator` - Interface for token generation
- `JwtTokenGenerator` - Implementation with GenerateAccessToken() and GenerateRefreshToken()

### New Types to Create

**Service Command/Result Types** (in RSSVibe.Services/Auth/):

1. **RefreshTokenCommand.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Command to refresh access token using refresh token.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken
);
```

2. **RefreshTokenResult.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of refresh token operation.
/// </summary>
public sealed record RefreshTokenResult
{
    public bool Success { get; init; }
    public RefreshTokenError? Error { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresInSeconds { get; init; }
    public bool MustChangePassword { get; init; }
}
```

3. **RefreshTokenError.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Error codes for refresh token operation.
/// </summary>
public enum RefreshTokenError
{
    /// <summary>
    /// Token not found, expired, or revoked.
    /// </summary>
    TokenInvalid,

    /// <summary>
    /// Token has already been used - replay attack detected.
    /// All user tokens have been revoked for security.
    /// </summary>
    TokenReplayDetected,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    ServiceUnavailable
}
```

## 4. Response Details

### Success Response (200 OK)

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "new-base64-encoded-string",
  "expiresInSeconds": 900,
  "mustChangePassword": false
}
```

**Response Type**: `RefreshTokenResponse`
- `accessToken`: New JWT access token (15-30 minute expiration)
- `refreshToken`: New refresh token (different from request token due to rotation)
- `expiresInSeconds`: Access token lifetime in seconds (typically 900-1800)
- `mustChangePassword`: Flag indicating user must change password before accessing other resources

### Error Responses

#### 400 Bad Request
**Trigger**: Invalid request payload (empty or missing refreshToken)
**Handling**: Automatic via FluentValidation
**Response**: ProblemDetails with validation errors

#### 401 Unauthorized
**Triggers**:
- Token not found in database
- Token is expired (ExpiresAt < UtcNow)
- Token is revoked (RevokedAt is not null)

**Response**:
```json
{
  "type": "about:blank",
  "title": "Invalid token",
  "status": 401,
  "detail": "The provided refresh token is invalid or has expired."
}
```

**Security Note**: Use generic error message to prevent token enumeration attacks. Don't reveal whether token doesn't exist, is expired, or is revoked.

#### 409 Conflict
**Trigger**: Token replay detected (IsUsed flag is true)

**Response**:
```json
{
  "type": "about:blank",
  "title": "Token reuse detected",
  "status": 409,
  "detail": "The refresh token has already been used. All tokens have been revoked for security. Please log in again."
}
```

**Security Action**: When this occurs, the system:
1. Detects that IsUsed = true (token previously used)
2. Revokes ALL refresh tokens for the user
3. Forces user to re-authenticate
4. Logs security event for monitoring

**Why 409**: This is a conflict with the one-time-use constraint and requires different client handling (force re-login) than a simple 401.

#### 503 Service Unavailable
**Trigger**: Database connection failure or other infrastructure issues

**Response**:
```json
{
  "type": "about:blank",
  "title": "Service temporarily unavailable",
  "status": 503,
  "detail": "Unable to process token refresh. Please try again later."
}
```

## 5. Data Flow

### Successful Refresh Flow

1. **Request Reception**:
   - Client sends POST to `/api/v1/auth/refresh` with refresh token
   - ASP.NET Core model binding deserializes request
   - FluentValidation validates request (non-empty token)

2. **Endpoint Processing** (RefreshEndpoint):
   - Map `RefreshTokenRequest` to `RefreshTokenCommand`
   - Call `IAuthService.RefreshTokenAsync(command, cancellationToken)`
   - Await service result

3. **Service Processing** (AuthService.RefreshTokenAsync):

   a. **Token Lookup**:
   ```csharp
   var refreshToken = await dbContext.RefreshTokens
       .Include(rt => rt.User)
       .FirstOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken);
   ```

   b. **Validation Checks** (short-circuit on first failure):
   - If token is null → return TokenInvalid
   - If token.ExpiresAt < DateTime.UtcNow → return TokenInvalid
   - If token.RevokedAt != null → return TokenInvalid

   c. **Replay Detection** (CRITICAL):
   ```csharp
   if (refreshToken.IsUsed)
   {
       // REPLAY ATTACK DETECTED
       // Revoke all tokens for this user as security measure
       await RevokeAllUserTokensAsync(refreshToken.UserId, cancellationToken);
       return new RefreshTokenResult
       {
           Success = false,
           Error = RefreshTokenError.TokenReplayDetected
       };
   }
   ```

   d. **Token Rotation** (within execution strategy and transaction):
   ```csharp
   // CRITICAL: Use execution strategy for PostgreSQL with state parameters to avoid closures
   var strategy = dbContext.Database.CreateExecutionStrategy();
   var state = (refreshToken, user, jwtTokenGenerator, dbContext, logger);

   return await strategy.ExecuteAsync(
       state,
       static async (state, ct) =>
       {
           var (refreshToken, user, jwtTokenGenerator, dbContext, logger) = state;

           await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
           try
           {
               // Mark old token as used
               refreshToken.IsUsed = true;

               // Generate new tokens
               var (accessToken, expiresInSeconds) = jwtTokenGenerator.GenerateAccessToken(user);
               var newRefreshTokenString = jwtTokenGenerator.GenerateRefreshToken();

               // Store new refresh token with same expiration strategy as original
               var newRefreshToken = new RefreshToken
               {
                   Id = Guid.CreateVersion7(),
                   UserId = refreshToken.UserId,
                   Token = newRefreshTokenString,
                   ExpiresAt = CalculateRefreshTokenExpiration(refreshToken),
                   CreatedAt = DateTime.UtcNow,
                   IsUsed = false
               };

               dbContext.RefreshTokens.Add(newRefreshToken);
               await dbContext.SaveChangesAsync(ct);
               await transaction.CommitAsync(ct);

               logger.LogInformation("Token refreshed successfully for user: {UserId}", refreshToken.UserId);

               return new RefreshTokenResult
               {
                   Success = true,
                   AccessToken = accessToken,
                   RefreshToken = newRefreshTokenString,
                   ExpiresInSeconds = expiresInSeconds,
                   MustChangePassword = user.MustChangePassword
               };
           }
           catch
           {
               await transaction.RollbackAsync(ct);
               throw;
           }
       },
       cancellationToken);
   ```

   **Note**: PostgreSQL EF Core provider requires wrapping transactions in execution strategy with state parameters to avoid closures. See Entity Framework docs for details.

4. **Response Generation** (RefreshEndpoint):
   - Map `RefreshTokenResult` to `RefreshTokenResponse`
   - Return TypedResults.Ok(response) or appropriate error

### Replay Attack Flow

1. Request with previously used token received
2. Service detects `IsUsed = true`
3. Service revokes all user tokens via `RevokeAllUserTokensAsync()`:
   ```csharp
   await dbContext.RefreshTokens
       .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
       .ExecuteUpdateAsync(rt => rt.SetProperty(x => x.RevokedAt, DateTime.UtcNow), cancellationToken);
   ```
4. Log security event with user ID and token ID
5. Return 409 Conflict with replay detection message
6. Client must re-authenticate with credentials

### Race Condition Handling

**Scenario**: Two concurrent refresh requests with same token

**Resolution**:
- Both requests read token with IsUsed = false
- First transaction commits: marks token as used, creates new token
- Second transaction commits: marks already-used token (no-op), creates duplicate new token
- Solution: Use transaction isolation level Read Committed (default) and check IsUsed again before commit
- Alternative: Use unique constraint on Token field to prevent duplicate active tokens

## 6. Security Considerations

### Authentication & Authorization
- **No traditional authentication required**: The refresh token itself authenticates the request
- **Token validation**: Comprehensive server-side validation (existence, expiration, revocation, usage)
- **User context**: Retrieved from database via token lookup (no reliance on client-provided data)

### Token Rotation (Security Best Practice)
- **One-time use**: Each refresh token can only be used once
- **Automatic rotation**: New refresh token generated on every refresh
- **Old token invalidation**: Previous token marked as used immediately
- **Prevents token reuse**: Even if token is stolen, it becomes invalid after first use

### Replay Attack Detection (Critical Security Feature)
- **Detection mechanism**: IsUsed flag on RefreshToken entity
- **Attack scenario**: Attacker steals refresh token and attempts to use it after legitimate user has already refreshed
- **Response to attack**:
  1. Detect IsUsed = true
  2. Revoke ALL user tokens (RevokedAt = UtcNow)
  3. Return 409 Conflict
  4. Force user to re-authenticate
  5. Log security event

**Why revoke all tokens?**
- If a token is reused, we can't determine which usage was legitimate
- Both the legitimate user and attacker may have active tokens
- Revoking all tokens and forcing re-authentication is the safest response
- User experiences minor inconvenience (re-login) but account remains secure

### Input Validation
- **Request validation**: FluentValidation ensures non-empty token
- **Token format**: No format validation needed (database lookup handles invalid formats)
- **Database validation**: Check existence, expiration, revocation, usage

### Timing Attack Mitigation
- **Generic error messages**: Don't reveal specific failure reasons (expired vs revoked vs not found)
- **Consistent response time**: Database query time dominates, reducing timing variance
- **Exception handling**: Catch and handle all exceptions consistently

### Token Security
- **Cryptographically secure generation**: 64 bytes from RandomNumberGenerator (512-bit entropy)
- **Server-side storage**: All token state stored in database (not in JWT)
- **Unique constraint**: Database enforces token uniqueness
- **Indexing**: Fast lookup without compromising security

### Logging & Monitoring
Log the following for security monitoring:
- All refresh attempts (success and failure)
- Token expiration events
- Token revocation events
- Replay attack detection (HIGH PRIORITY - potential security breach)
- Database errors

**Log Anonymization**: Use AnonymizeEmail() for email addresses in logs (first character + *** + domain)

**Do NOT log**:
- Refresh token values (sensitive credentials)
- Access token values (contain user data)
- Full email addresses (PII)

## 7. Error Handling

### Error Mapping Strategy

**Service Errors → HTTP Status Codes**:
| Service Error | HTTP Status | Client Action |
|---------------|-------------|---------------|
| TokenInvalid | 401 Unauthorized | Redirect to login |
| TokenReplayDetected | 409 Conflict | Force re-login with explanation |
| ServiceUnavailable | 503 Service Unavailable | Retry with exponential backoff |
| Unexpected exception | 500 Internal Server Error | Display error, log to monitoring |

### Exception Handling Strategy

1. **DbException** (Database errors):
   - Catch in service layer
   - Log full exception with context
   - Return ServiceUnavailable error
   - Map to 503 in endpoint

2. **OperationCanceledException** (Request cancelled):
   - Let ASP.NET Core handle automatically
   - No custom handling needed
   - Results in 499 Client Closed Request

3. **All other exceptions**:
   - Catch in service layer
   - Log full exception with stack trace
   - Return generic error
   - Map to 500 in endpoint

### Transaction Rollback

If any operation in the token rotation transaction fails:
1. Rollback transaction (old token NOT marked as used)
2. No new token created
3. Client can retry with same token
4. Log error for investigation

## 8. Performance Considerations

### Database Queries
- **Single query**: One query to fetch token with User (Include)
- **Indexed lookup**: Token field has unique index for O(1) lookup
- **Minimal joins**: Only join User table for mustChangePassword flag
- **Batch revocation**: Use ExecuteUpdateAsync for efficient multi-row revocation (replay scenario)

### Caching Strategy
- **No caching**: Refresh tokens cannot be cached due to one-time-use constraint
- **Token state must be real-time**: Must check database for IsUsed flag
- **User data caching**: Could cache ApplicationUser data, but complexity not worth benefit

### Transaction Overhead
- **Minimal transaction scope**: Only token rotation operations
- **Short transaction duration**: ~10ms typical duration
- **Read Committed isolation**: Default level, good balance of consistency and performance
- **Rollback rare**: Only on database errors or cancellation

### Scalability Considerations
- **Stateless operation**: No server-side state beyond database
- **Horizontal scaling**: Works across multiple API instances
- **Database bottleneck**: Database is single point of contention
  - Mitigation: Connection pooling (default in EF Core)
  - Mitigation: Database replication for read queries (future optimization)

### Expected Performance
- **P50 latency**: ~50ms (database query + token generation)
- **P95 latency**: ~150ms (includes occasional slow queries)
- **Throughput**: Limited by database, not application logic
- **Concurrency**: Handles concurrent requests via transaction isolation

## 9. Implementation Steps

### Step 1: Create Service Command/Result Types

**Files to create** in `/src/RSSVibe.Services/Auth/`:

1. **RefreshTokenCommand.cs**:
   - Simple record with RefreshToken property
   - No validation (handled by request DTO)

2. **RefreshTokenError.cs**:
   - Enum with: TokenInvalid, TokenReplayDetected, ServiceUnavailable

3. **RefreshTokenResult.cs**:
   - Success flag
   - Error (nullable enum)
   - AccessToken (nullable string)
   - RefreshToken (nullable string)
   - ExpiresInSeconds (int)
   - MustChangePassword (bool)

### Step 2: Extend IAuthService Interface

**File**: `/src/RSSVibe.Services/Auth/IAuthService.cs`

Add method signature:
```csharp
/// <summary>
/// Refreshes access token using refresh token with replay attack detection.
/// </summary>
/// <param name="command">Refresh token command.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Result containing new tokens or error information.</returns>
Task<RefreshTokenResult> RefreshTokenAsync(
    RefreshTokenCommand command,
    CancellationToken cancellationToken = default);
```

### Step 3: Implement RefreshTokenAsync in AuthService

**File**: `/src/RSSVibe.Services/Auth/AuthService.cs`

Implementation structure:
```csharp
public async Task<RefreshTokenResult> RefreshTokenAsync(
    RefreshTokenCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        // 1. Look up token with User
        var refreshToken = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken);

        // 2. Validate token existence
        if (refreshToken is null)
        {
            logger.LogInformation("Refresh attempt with non-existent token");
            return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
        }

        // 3. Validate token expiration
        if (refreshToken.ExpiresAt < DateTime.UtcNow)
        {
            logger.LogInformation("Refresh attempt with expired token for user: {UserId}", refreshToken.UserId);
            return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
        }

        // 4. Validate token not revoked
        if (refreshToken.RevokedAt is not null)
        {
            logger.LogInformation("Refresh attempt with revoked token for user: {UserId}", refreshToken.UserId);
            return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
        }

        // 5. CRITICAL: Check for replay attack
        if (refreshToken.IsUsed)
        {
            logger.LogWarning(
                "SECURITY: Token replay detected for user {UserId}. Revoking all tokens.",
                refreshToken.UserId);

            // Revoke all user tokens
            await dbContext.RefreshTokens
                .Where(rt => rt.UserId == refreshToken.UserId && rt.RevokedAt == null)
                .ExecuteUpdateAsync(
                    rt => rt.SetProperty(x => x.RevokedAt, DateTime.UtcNow),
                    cancellationToken);

            return new RefreshTokenResult
            {
                Success = false,
                Error = RefreshTokenError.TokenReplayDetected
            };
        }

        // 6. Use execution strategy for PostgreSQL transaction handling (CRITICAL for PostgreSQL)
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 7. Mark old token as used
                refreshToken.IsUsed = true;

                // 8. Generate new access token
                var (accessToken, expiresInSeconds) = jwtTokenGenerator.GenerateAccessToken(refreshToken.User);

                // 9. Generate new refresh token
                var newRefreshTokenString = jwtTokenGenerator.GenerateRefreshToken();

                // 10. Calculate new refresh token expiration (sliding window)
                var newExpiration = DateTime.UtcNow.AddDays(
                    (refreshToken.ExpiresAt - refreshToken.CreatedAt).TotalDays);

                // 11. Create new refresh token entity
                var newRefreshToken = new RefreshToken
                {
                    Id = Guid.CreateVersion7(),
                    UserId = refreshToken.UserId,
                    Token = newRefreshTokenString,
                    ExpiresAt = newExpiration,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false
                };

                dbContext.RefreshTokens.Add(newRefreshToken);

                // 12. Save changes and commit transaction
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Token refreshed successfully for user: {UserId}",
                    refreshToken.UserId);

                // 13. Return success result
                return new RefreshTokenResult
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = newRefreshTokenString,
                    ExpiresInSeconds = expiresInSeconds,
                    MustChangePassword = refreshToken.User.MustChangePassword
                };
            }
            catch
            {
                // Rollback transaction on error
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
    catch (DbException ex)
    {
        logger.LogError(ex, "Database error during token refresh");
        return new RefreshTokenResult
        {
            Success = false,
            Error = RefreshTokenError.ServiceUnavailable
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during token refresh");
        return new RefreshTokenResult
        {
            Success = false,
            Error = RefreshTokenError.ServiceUnavailable
        };
    }
}
```

### Step 4: Create RefreshEndpoint

**File**: `/src/RSSVibe.ApiService/Endpoints/Auth/RefreshEndpoint.cs`

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for refreshing access tokens using refresh tokens.
/// </summary>
public static class RefreshEndpoint
{
    /// <summary>
    /// Maps the refresh endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapRefreshEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Refresh access token";
                operation.Description = "Exchanges a refresh token for a new access token and refresh token. " +
                    "Implements token rotation for enhanced security. " +
                    "Detects and prevents token replay attacks by revoking all tokens on reuse attempts.";
                return operation;
            })
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<
        Ok<RefreshTokenResponse>,
        ProblemHttpResult>>
        HandleAsync(
            RefreshTokenRequest request,
            IAuthService authService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RefreshEndpoint");

        // Map request to command
        var command = new RefreshTokenCommand(request.RefreshToken);

        // Call service
        var result = await authService.RefreshTokenAsync(command, cancellationToken);

        // Handle errors
        if (!result.Success)
        {
            return result.Error switch
            {
                RefreshTokenError.TokenInvalid => TypedResults.Problem(
                    title: "Invalid token",
                    detail: "The provided refresh token is invalid or has expired.",
                    statusCode: StatusCodes.Status401Unauthorized),

                RefreshTokenError.TokenReplayDetected => TypedResults.Problem(
                    title: "Token reuse detected",
                    detail: "The refresh token has already been used. All tokens have been revoked for security. Please log in again.",
                    statusCode: StatusCodes.Status409Conflict),

                RefreshTokenError.ServiceUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to process token refresh. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Token refresh failed",
                    detail: "An unexpected error occurred during token refresh.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // Success - return 200 OK with new tokens
        var response = new RefreshTokenResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresInSeconds,
            result.MustChangePassword);

        logger.LogInformation("Token refreshed successfully");

        return TypedResults.Ok(response);
    }
}
```

### Step 5: Register Endpoint in AuthGroup

**File**: `/src/RSSVibe.ApiService/Endpoints/Auth/AuthGroup.cs`

Add line in MapAuthGroup method:
```csharp
public static IEndpointRouteBuilder MapAuthGroup(
    this IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/auth")
        .WithTags("Auth");

    // Register all endpoints in the auth group
    group.MapRegisterEndpoint();
    group.MapLoginEndpoint();
    group.MapRefreshEndpoint(); // ADD THIS LINE

    return endpoints;
}
```

### Step 6: Write Integration Tests

**File**: `/tests/RSSVibe.ApiService.Tests/Endpoints/Auth/RefreshEndpointTests.cs`

Test scenarios to implement:

1. **Refresh_ValidToken_ReturnsNewTokens**:
   - Create user and login to get initial tokens
   - Call refresh endpoint with refresh token
   - Assert 200 OK response
   - Assert new access and refresh tokens returned
   - Assert old refresh token marked as used in database
   - Assert new refresh token exists in database

2. **Refresh_ExpiredToken_Returns401**:
   - Create refresh token with ExpiresAt in past
   - Call refresh endpoint
   - Assert 401 Unauthorized response

3. **Refresh_RevokedToken_Returns401**:
   - Create refresh token with RevokedAt set
   - Call refresh endpoint
   - Assert 401 Unauthorized response

4. **Refresh_NonExistentToken_Returns401**:
   - Call refresh endpoint with random token string
   - Assert 401 Unauthorized response

5. **Refresh_UsedToken_Returns409AndRevokesAllTokens**:
   - Create user and login to get token
   - Refresh once (marks token as used)
   - Create second refresh token for same user (simulate concurrent session)
   - Attempt to refresh with first (used) token
   - Assert 409 Conflict response
   - Assert both refresh tokens are revoked in database

6. **Refresh_EmptyToken_Returns400**:
   - Call refresh endpoint with empty token string
   - Assert 400 Bad Request (FluentValidation)

7. **Refresh_PreservesMustChangePassword**:
   - Create user with MustChangePassword = true
   - Login and refresh
   - Assert response has MustChangePassword = true

8. **Refresh_SlidingExpiration**:
   - Create refresh token with 30-day expiration
   - Refresh token
   - Assert new refresh token has same expiration duration (~30 days from now)

### Step 7: Manual Testing Checklist

After implementation, verify manually:

1. Obtain initial tokens via login endpoint
2. Use refresh token to get new tokens
3. Verify new access token works for authenticated endpoints
4. Verify old refresh token cannot be reused (409 error)
5. Test with Postman/curl to verify request/response formats
6. Check database to verify token rotation (IsUsed flag, new token created)
7. Test concurrent refresh with same token (race condition)
8. Verify logging output for all scenarios

### Step 8: Documentation Updates

No additional documentation required:
- Endpoint automatically included in OpenAPI/Swagger documentation
- Implementation plan serves as comprehensive reference
- Code comments provide inline documentation

---

## Summary

This implementation plan provides a comprehensive guide for implementing the POST /api/v1/auth/refresh endpoint with:

- Secure token rotation (one-time-use pattern)
- Replay attack detection and mitigation
- Proper error handling and status codes
- Transaction-based atomicity
- Comprehensive logging and monitoring
- Performance optimization
- Full test coverage

The implementation follows all project conventions:
- Minimal APIs with hierarchical group structure
- Service layer separation (AuthService)
- TypedResults for type-safe responses
- FluentValidation for request validation
- TUnit for integration testing
- Modern C# patterns (records, primary constructors, pattern matching)

**Security is paramount**: The replay detection mechanism protects against token theft by revoking all user tokens when reuse is detected, forcing re-authentication and alerting to potential security breach.
