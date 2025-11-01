# API Endpoint Implementation Plan: POST /api/v1/auth/change-password

## 1. Endpoint Overview

This endpoint enables authenticated users to change their password by providing their current password and a new password. It is particularly important for bootstrapped root users who are provisioned via environment variables and must change their password on first login.

**Key Responsibilities**:
- Verify the user's current password for security
- Validate the new password against complexity requirements
- Update the user's password in the ASP.NET Identity store
- Clear the `MustChangePassword` flag when set
- Revoke all existing refresh tokens to invalidate previous sessions (security best practice)
- Return 204 No Content on success (no response body needed)

**Authentication Required**: Yes (JWT Bearer token)

**Rate Limiting**: Yes (to prevent brute force attacks on password verification)

---

## 2. Request Details

- **HTTP Method**: `POST`
- **URL Structure**: `/api/v1/auth/change-password`
- **Authentication**: Required (JWT Bearer token in `Authorization` header)
- **Content-Type**: `application/json`

### Parameters

**Required** (in request body):
- `currentPassword` (string): User's existing password for verification
  - Constraints: Length 12-100 characters
  - Must match user's actual current password

- `newPassword` (string): New password to set
  - Constraints: Length 12-100 characters
  - Must contain: uppercase letter, lowercase letter, digit, special character
  - Must be different from `currentPassword`

**Optional**: None

### Request Body Example

```json
{
  "currentPassword": "OldPass123!Secure",
  "newPassword": "NewPass456!MoreSecure"
}
```

---

## 3. Used Types

### Contract Types (RSSVibe.Contracts/Auth/)

#### ChangePasswordRequest.cs
```csharp
namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request model for changing user password.
/// </summary>
/// <param name="CurrentPassword">User's current password for verification.</param>
/// <param name="NewPassword">New password meeting complexity requirements.</param>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword)
{
    /// <summary>
    /// Validator for password change requests.
    /// </summary>
    public sealed class Validator : AbstractValidator<ChangePasswordRequest>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty()
                .WithMessage("Current password is required.")
                .Length(12, 100)
                .WithMessage("Current password must be between 12 and 100 characters.");

            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .WithMessage("New password is required.")
                .Length(12, 100)
                .WithMessage("New password must be between 12 and 100 characters.")
                .Matches(@"[A-Z]")
                .WithMessage("New password must contain at least one uppercase letter.")
                .Matches(@"[a-z]")
                .WithMessage("New password must contain at least one lowercase letter.")
                .Matches(@"[0-9]")
                .WithMessage("New password must contain at least one digit.")
                .Matches(@"[^a-zA-Z0-9]")
                .WithMessage("New password must contain at least one special character.");

            RuleFor(x => x)
                .Must(x => x.NewPassword != x.CurrentPassword)
                .WithMessage("New password must be different from current password.")
                .WithName("NewPassword");
        }
    }
}
```

### Service Layer Types (RSSVibe.Services/Auth/)

#### ChangePasswordCommand.cs (or inline in IAuthService.cs)
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for changing a user's password.
/// </summary>
/// <param name="UserId">ID of the user changing their password.</param>
/// <param name="CurrentPassword">Current password for verification.</param>
/// <param name="NewPassword">New password to set.</param>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);
```

#### ChangePasswordResult.cs (or inline in IAuthService.cs)
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of password change operation.
/// </summary>
public sealed record ChangePasswordResult
{
    /// <summary>
    /// Indicates whether the password change was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error details if the operation failed.
    /// </summary>
    public ChangePasswordError? Error { get; init; }
}
```

#### ChangePasswordError.cs (or inline in IAuthService.cs)
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Possible errors during password change.
/// </summary>
public enum ChangePasswordError
{
    /// <summary>
    /// The current password provided is incorrect.
    /// </summary>
    InvalidCurrentPassword,

    /// <summary>
    /// The new password does not meet complexity requirements.
    /// </summary>
    WeakPassword,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    IdentityStoreUnavailable
}
```

---

## 4. Response Details

### Success Response
- **Status Code**: `204 No Content`
- **Body**: Empty (no response body)
- **Headers**: None (standard HTTP headers only)

### Error Responses

#### 400 Bad Request
Returned when validation fails (FluentValidation or Identity password policy).

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "NewPassword": [
      "New password must contain at least one uppercase letter."
    ]
  }
}
```

#### 401 Unauthorized
Returned when:
- No JWT token provided
- JWT token is invalid or expired
- Current password is incorrect

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Authentication failed",
  "status": 401,
  "detail": "Current password is incorrect."
}
```

#### 429 Too Many Requests
Returned when rate limit is exceeded (e.g., 5 attempts per 15 minutes per user).

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too many requests",
  "status": 429,
  "detail": "Too many password change attempts. Please try again later.",
  "retryAfter": 900
}
```

#### 503 Service Unavailable
Returned when database or Identity store is unreachable.

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service temporarily unavailable",
  "status": 503,
  "detail": "Unable to connect to the identity store. Please try again later."
}
```

---

## 5. Data Flow

```
┌─────────┐
│ Client  │
└────┬────┘
     │ 1. POST /api/v1/auth/change-password
     │    Authorization: Bearer <JWT>
     │    Body: { currentPassword, newPassword }
     ▼
┌─────────────────────────┐
│ ASP.NET Core Pipeline   │
├─────────────────────────┤
│ 2. JWT Authentication   │◄─── Extract userId from claims
│ 3. Rate Limiting        │◄─── Check attempt count
│ 4. FluentValidation     │◄─── Validate request model
└────┬────────────────────┘
     │ 5. Valid authenticated request
     ▼
┌──────────────────────────┐
│ ChangePasswordEndpoint   │
├──────────────────────────┤
│ • Extract userId from    │
│   ClaimsPrincipal        │
│ • Map to command         │
└────┬─────────────────────┘
     │ 6. ChangePasswordCommand
     ▼
┌──────────────────────────┐
│ AuthService              │
├──────────────────────────┤
│ 7. Get user by ID        │──┐
│ 8. Verify current pwd    │  │
│ 9. Change password       │  │
│ 10. Clear MustChange flag│  │
│ 11. Revoke refresh tokens│  │
└────┬─────────────────────┘  │
     │                         │
     │ 12. Success/Error       │
     ▼                         │
┌──────────────────────────┐  │
│ ChangePasswordEndpoint   │  │
├──────────────────────────┤  │
│ • Map result to response │  │
│ • Return 204 or error    │  │
└────┬─────────────────────┘  │
     │ 13. HTTP Response       │
     ▼                         │
┌─────────┐                    │
│ Client  │                    │
└─────────┘                    │
                               │
     ┌─────────────────────────┘
     │ Database Interactions
     ▼
┌──────────────────────────┐
│ PostgreSQL Database      │
├──────────────────────────┤
│ • AspNetUsers (update)   │
│ • RefreshTokens (update) │
└──────────────────────────┘
```

### Detailed Flow Steps

1. **Client Request**: Client sends POST request with current and new passwords, includes JWT token
2. **JWT Authentication**: ASP.NET Core JWT middleware validates token and populates `HttpContext.User`
3. **Rate Limiting**: Custom rate limiting middleware checks if user has exceeded password change attempts (5 per 15 min)
4. **FluentValidation**: Automatic validation runs before endpoint handler (via SharpGrip.FluentValidation.AutoValidation)
5. **Endpoint Handler**: Extracts `userId` from JWT claims, creates command, calls service
6. **Service - Load User**: `UserManager.FindByIdAsync(userId)` retrieves user entity
7. **Service - Verify Password**: `UserManager.CheckPasswordAsync(user, currentPassword)` validates current password
8. **Service - Change Password**: `UserManager.ChangePasswordAsync(user, currentPassword, newPassword)` updates password hash
9. **Service - Clear Flag**: Set `user.MustChangePassword = false` and save via DbContext
10. **Service - Revoke Tokens**: Update all active refresh tokens: `SET RevokedAt = NOW() WHERE UserId = @userId AND RevokedAt IS NULL`
11. **Service - Commit**: `DbContext.SaveChangesAsync()` commits all changes in transaction
12. **Service Result**: Return success/error result to endpoint
13. **HTTP Response**: Endpoint returns 204 No Content or appropriate error status code

---

## 6. Security Considerations

### Authentication & Authorization
- **JWT Required**: Endpoint MUST require valid JWT Bearer token (use `.RequireAuthorization()`)
- **User Context**: Extract `userId` from `ClaimsPrincipal` (`User.FindFirstValue(ClaimTypes.NameIdentifier)`)
- **No Elevation**: Users can only change their own password (derived from JWT claims)

### Password Security
- **Current Password Verification**: MUST verify current password before allowing change (prevents unauthorized access if session is hijacked)
- **Password Hashing**: ASP.NET Identity handles secure hashing (PBKDF2 by default)
- **Password Policy**: Enforced by Identity options (12+ chars, complexity requirements)
- **No Password Logging**: NEVER log passwords in any form (current or new)

### Session Management
- **Token Revocation**: MUST revoke all refresh tokens on password change to invalidate existing sessions
- **Security Rationale**: If password was compromised, all previous sessions should be terminated
- **Implementation**: Update `RefreshTokens` table: `SET RevokedAt = CURRENT_TIMESTAMP WHERE UserId = @userId AND RevokedAt IS NULL`

### Rate Limiting
- **Protection**: Prevent brute force attacks on current password verification
- **Strategy**: Per-user rate limiting (5 attempts per 15 minutes)
- **Implementation**: Use ASP.NET Core rate limiting middleware with user identifier from JWT claims
- **Response**: Return 429 Too Many Requests with `Retry-After` header

### Additional Considerations
- **HTTPS Only**: Endpoint must only be accessible over HTTPS (enforced at infrastructure/reverse proxy level)
- **Timing Attacks**: Use constant-time password comparison (handled by `UserManager.CheckPasswordAsync`)
- **Account Lockout**: Consider integrating with existing Identity lockout mechanism (already configured in Program.cs)
- **Audit Logging**: Log successful password changes with anonymized user identifier for security audit trail

---

## 7. Error Handling

### Error Scenarios and Responses

| Scenario | Status Code | Error Type | Client Message |
|----------|-------------|------------|----------------|
| No JWT token provided | 401 | Unauthorized | "Authorization header is missing or invalid." |
| JWT token expired | 401 | Unauthorized | "Token has expired. Please log in again." |
| JWT token invalid | 401 | Unauthorized | "Invalid authentication token." |
| Current password incorrect | 401 | Unauthorized | "Current password is incorrect." |
| New password too short | 400 | Bad Request | "New password must be between 12 and 100 characters." |
| New password missing uppercase | 400 | Bad Request | "New password must contain at least one uppercase letter." |
| New password missing lowercase | 400 | Bad Request | "New password must contain at least one lowercase letter." |
| New password missing digit | 400 | Bad Request | "New password must contain at least one digit." |
| New password missing special char | 400 | Bad Request | "New password must contain at least one special character." |
| New password same as current | 400 | Bad Request | "New password must be different from current password." |
| Rate limit exceeded | 429 | Too Many Requests | "Too many password change attempts. Please try again later." |
| Database connection failure | 503 | Service Unavailable | "Unable to connect to the identity store. Please try again later." |
| Identity store error | 503 | Service Unavailable | "Service is temporarily unavailable. Please try again later." |

### Logging Strategy

#### Success Scenario
```csharp
logger.LogInformation(
    "Password changed successfully for user: {UserId}",
    userId);
```

#### Failed Current Password
```csharp
logger.LogWarning(
    "Failed password change attempt for user {UserId}: Invalid current password",
    userId);
```

#### Rate Limit Exceeded
```csharp
logger.LogWarning(
    "Rate limit exceeded for password change: {UserId}",
    userId);
```

#### Database Error
```csharp
logger.LogError(
    ex,
    "Database error during password change for user {UserId}",
    userId);
```

#### Identity Error
```csharp
logger.LogError(
    "Identity error during password change for user {UserId}: {Errors}",
    userId,
    string.Join(", ", identityResult.Errors.Select(e => e.Description)));
```

**Important**: NEVER log passwords (current or new) in any log statements.

---

## 8. Performance Considerations

### Potential Bottlenecks
1. **Password Hashing**: PBKDF2 hashing is computationally expensive (by design for security)
   - **Impact**: Each password change takes ~100-500ms
   - **Mitigation**: This is expected and necessary for security; no optimization needed

2. **Database Roundtrips**: Multiple database calls (get user, update user, revoke tokens)
   - **Impact**: 3 database roundtrips per request
   - **Mitigation**: Use single transaction, consider combining updates if performance becomes issue

3. **Refresh Token Revocation**: May need to update multiple rows if user has many active sessions
   - **Impact**: O(n) where n = number of active refresh tokens
   - **Mitigation**: Add database index on `(UserId, RevokedAt)` for efficient updates

### Optimization Strategies

1. **Database Transaction**: Wrap all operations in single transaction to ensure atomicity
```csharp
using var transaction = await dbContext.Database.BeginTransactionAsync();
try
{
    // Change password
    // Clear MustChangePassword flag
    // Revoke tokens
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

2. **Bulk Token Revocation**: Use single UPDATE query to revoke all tokens
```csharp
await dbContext.RefreshTokens
    .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow));
```

3. **Database Indexes**: Ensure proper indexes exist
```sql
-- Already should exist from RefreshTokenConfiguration
CREATE INDEX idx_refreshtokens_userid_revokedat
ON "RefreshTokens" ("UserId", "RevokedAt");
```

4. **Caching**: DO NOT cache password-related operations (security risk)

### Expected Performance
- **Typical Response Time**: 150-600ms (dominated by password hashing)
- **Database Load**: Low (3 queries per request, infrequent operation)
- **Concurrency**: Low (users rarely change passwords simultaneously)
- **Rate Limiting Impact**: Minimal (applies per user, not globally)

---

## 9. Implementation Steps

### Step 1: Create Contract Types
**Location**: `src/RSSVibe.Contracts/Auth/ChangePasswordRequest.cs`

1. Create `ChangePasswordRequest` positional record with two string parameters
2. Add nested `Validator` class extending `AbstractValidator<ChangePasswordRequest>`
3. Implement validation rules:
   - CurrentPassword: NotEmpty, Length(12, 100)
   - NewPassword: NotEmpty, Length(12, 100), regex matches for complexity
   - Cross-field: NewPassword != CurrentPassword
4. Add XML documentation comments

**Expected Outcome**: Request model with embedded FluentValidation validator ready for automatic validation.

---

### Step 2: Add Service Layer Types
**Location**: `src/RSSVibe.Services/Auth/` (IAuthService.cs and related files)

1. Create `ChangePasswordCommand` record:
   - Properties: UserId (Guid), CurrentPassword (string), NewPassword (string)
   - Add XML documentation

2. Create `ChangePasswordError` enum:
   - Values: InvalidCurrentPassword, WeakPassword, IdentityStoreUnavailable
   - Add XML documentation

3. Create `ChangePasswordResult` record:
   - Properties: Success (bool), Error (ChangePasswordError?)
   - Add XML documentation

4. Update `IAuthService` interface:
   - Add method signature: `Task<ChangePasswordResult> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)`
   - Add XML documentation

**Expected Outcome**: Service contract and models defined, ready for implementation.

---

### Step 3: Implement Service Method
**Location**: `src/RSSVibe.Services/Auth/AuthService.cs`

1. Implement `ChangePasswordAsync` method in `AuthService` class

2. **Algorithm**:
   ```
   a. Find user by ID using UserManager.FindByIdAsync(userId)
      - Return InvalidCurrentPassword error if user not found (don't reveal user doesn't exist)

   b. Verify current password using UserManager.CheckPasswordAsync(user, currentPassword)
      - Return InvalidCurrentPassword error if verification fails

   c. Change password using UserManager.ChangePasswordAsync(user, currentPassword, newPassword)
      - Return WeakPassword error if Identity validation fails
      - Wrap in try-catch for DbException

   d. Clear MustChangePassword flag
      - Set user.MustChangePassword = false
      - Call UserManager.UpdateAsync(user) or DbContext.SaveChangesAsync()

   e. Revoke all active refresh tokens
      - Use ExecuteUpdateAsync to set RevokedAt = UtcNow WHERE UserId = userId AND RevokedAt IS NULL

   f. Return success result
   ```

3. **Error Handling**:
   - Catch `DbException`: Return IdentityStoreUnavailable error
   - Catch `Exception`: Log and return IdentityStoreUnavailable error
   - Log all errors with anonymized userId (NOT passwords)

4. **Logging**:
   ```csharp
   // Success
   logger.LogInformation("Password changed successfully for user: {UserId}", userId);

   // Invalid current password
   logger.LogWarning("Failed password change attempt for user {UserId}: Invalid current password", userId);

   // Database error
   logger.LogError(ex, "Database error during password change for user {UserId}", userId);
   ```

**Expected Outcome**: Fully implemented service method with proper error handling, logging, and token revocation.

---

### Step 4: Create Endpoint
**Location**: `src/RSSVibe.ApiService/Endpoints/Auth/ChangePasswordEndpoint.cs`

1. Create static class `ChangePasswordEndpoint`

2. Create `MapChangePasswordEndpoint(RouteGroupBuilder)` extension method:
   - Map POST route: `group.MapPost("/change-password", HandleAsync)`
   - Add authorization: `.RequireAuthorization()`
   - Configure OpenAPI metadata: `.WithName("ChangePassword")`, `.WithOpenApi(...)`
   - Document responses:
     - `.Produces(StatusCodes.Status204NoContent)`
     - `.ProducesProblem(StatusCodes.Status400BadRequest)`
     - `.ProducesProblem(StatusCodes.Status401Unauthorized)`
     - `.ProducesProblem(StatusCodes.Status429TooManyRequests)`
     - `.ProducesProblem(StatusCodes.Status503ServiceUnavailable)`

3. Implement `HandleAsync` private method:
   ```csharp
   private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
       ChangePasswordRequest request,
       ClaimsPrincipal user,
       IAuthService authService,
       ILoggerFactory loggerFactory,
       CancellationToken cancellationToken)
   ```

4. **Handler Logic**:
   ```
   a. Extract userId from ClaimsPrincipal
      - user.FindFirstValue(ClaimTypes.NameIdentifier)
      - If null, return 401 Unauthorized (should never happen due to RequireAuthorization)

   b. Create command
      - new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword)

   c. Call service
      - var result = await authService.ChangePasswordAsync(command, cancellationToken)

   d. Handle result
      - Success: return TypedResults.NoContent()
      - InvalidCurrentPassword: return TypedResults.Problem(title: "Authentication failed", detail: "Current password is incorrect.", statusCode: 401)
      - WeakPassword: return TypedResults.Problem(title: "Invalid password", detail: "New password does not meet complexity requirements.", statusCode: 400)
      - IdentityStoreUnavailable: return TypedResults.Problem(title: "Service temporarily unavailable", detail: "Unable to connect to the identity store. Please try again later.", statusCode: 503)
   ```

5. **Return Type**: `Results<NoContent, ProblemHttpResult>` for type-safe responses

**Expected Outcome**: Endpoint handler that maps HTTP requests to service calls and service results to HTTP responses.

---

### Step 5: Register Endpoint in AuthGroup
**Location**: `src/RSSVibe.ApiService/Endpoints/Auth/AuthGroup.cs`

1. Add line to `MapAuthGroup` method:
   ```csharp
   group.MapChangePasswordEndpoint();
   ```

2. Position: Add after existing endpoint registrations (e.g., after `MapLoginEndpoint()`)

**Expected Outcome**: Endpoint registered and accessible at `/api/v1/auth/change-password`.

---

### Step 6: Configure Rate Limiting
**Location**: `src/RSSVibe.ApiService/Program.cs`

1. Add rate limiting services:
   ```csharp
   builder.Services.AddRateLimiter(options =>
   {
       options.AddPolicy("password-change", context =>
       {
           var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
           return RateLimitPartition.GetFixedWindowLimiter(
               partitionKey: userId ?? "anonymous",
               factory: _ => new FixedWindowRateLimiterOptions
               {
                   PermitLimit = 5,
                   Window = TimeSpan.FromMinutes(15),
                   QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                   QueueLimit = 0 // Reject immediately if limit exceeded
               });
       });
   });
   ```

2. Apply rate limiting middleware:
   ```csharp
   app.UseRateLimiter();
   ```
   **Position**: After `UseRouting()` and before `MapApiV1()`

3. Update endpoint registration to apply rate limiting policy:
   ```csharp
   // In ChangePasswordEndpoint.cs
   group.MapPost("/change-password", HandleAsync)
       .RequireAuthorization()
       .RequireRateLimiting("password-change") // Add this
       // ... other configurations
   ```

**Expected Outcome**: Rate limiting enforced at 5 requests per 15 minutes per user for password change endpoint.

---

### Step 7: Database Migration (if needed)
**Location**: `src/RSSVibe.Data/`

**Check**: Verify RefreshToken entity has index on (UserId, RevokedAt)

1. Review `src/RSSVibe.Data/Configurations/RefreshTokenConfiguration.cs`
2. Ensure index exists:
   ```csharp
   builder.HasIndex(rt => new { rt.UserId, rt.RevokedAt })
       .HasDatabaseName("IX_RefreshTokens_UserId_RevokedAt");
   ```

3. If index is missing:
   - Add index configuration
   - Run migration: `cd src/RSSVibe.Data && bash add_migration.sh AddRefreshTokenUserIdRevokedAtIndex`

**Expected Outcome**: Database schema supports efficient refresh token revocation queries.

---

### Step 8: Add Integration Tests
**Location**: `tests/RSSVibe.ApiService.Tests/Endpoints/Auth/ChangePasswordEndpointTests.cs`

Create test class with the following test methods:

1. **ChangePassword_ValidRequest_Returns204**
   - Create authenticated client
   - Register user with initial password
   - Send valid password change request
   - Assert: 204 No Content response
   - Verify: Can login with new password
   - Verify: Cannot login with old password
   - Verify: MustChangePassword flag is cleared

2. **ChangePassword_InvalidCurrentPassword_Returns401**
   - Create authenticated client
   - Send request with wrong current password
   - Assert: 401 Unauthorized response
   - Assert: Response contains "Current password is incorrect" message

3. **ChangePassword_WeakNewPassword_Returns400**
   - Create authenticated client
   - Send request with weak new password (e.g., "short")
   - Assert: 400 Bad Request response
   - Assert: Validation errors returned

4. **ChangePassword_NewPasswordSameAsCurrent_Returns400**
   - Create authenticated client
   - Send request where newPassword == currentPassword
   - Assert: 400 Bad Request response
   - Assert: Error message indicates passwords must be different

5. **ChangePassword_UnauthorizedRequest_Returns401**
   - Create unauthenticated client (no JWT token)
   - Send password change request
   - Assert: 401 Unauthorized response

6. **ChangePassword_RevokesPreviousRefreshTokens**
   - Create authenticated client
   - Login to obtain refresh token
   - Change password
   - Attempt to use old refresh token
   - Assert: Old refresh token is rejected

7. **ChangePassword_ClearsMustChangePasswordFlag**
   - Bootstrap root user with MustChangePassword = true
   - Login as root user
   - Change password
   - Verify: MustChangePassword flag is false in database
   - Verify: Can access other endpoints without forced password change

8. **ChangePassword_RateLimiting_Returns429**
   - Create authenticated client
   - Send 6 password change requests in quick succession
   - Assert: 6th request returns 429 Too Many Requests
   - Assert: Response includes Retry-After header

**Testing Patterns**:
```csharp
// Create authenticated client for user
var client = await WebApplicationFactory.CreateAuthenticatedClientAsync();

// Send password change request
var request = new ChangePasswordRequest(
    CurrentPassword: "OldPass123!",
    NewPassword: "NewPass456!More"
);
var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);

// Assert response status
await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

// Verify password changed in database
await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var user = await userManager.FindByEmailAsync(TestApplication.TestUserEmail);
var passwordValid = await userManager.CheckPasswordAsync(user!, "NewPass456!More");
await Assert.That(passwordValid).IsTrue();
```

**Expected Outcome**: Comprehensive test coverage ensuring endpoint works correctly and securely.

---

### Step 9: Manual Testing Checklist

1. **Successful Password Change**:
   - Register new user
   - Login to get JWT token
   - Call change-password endpoint with valid credentials
   - Verify 204 response
   - Verify can login with new password
   - Verify cannot login with old password

2. **Security Validation**:
   - Attempt password change without JWT token → expect 401
   - Attempt password change with expired JWT → expect 401
   - Attempt password change with wrong current password → expect 401
   - Verify old refresh token is revoked after password change

3. **Password Policy Enforcement**:
   - Try new password < 12 chars → expect 400
   - Try new password without uppercase → expect 400
   - Try new password without lowercase → expect 400
   - Try new password without digit → expect 400
   - Try new password without special char → expect 400
   - Try new password same as current → expect 400

4. **Rate Limiting**:
   - Make 5 password change attempts quickly → all succeed or fail based on validity
   - Make 6th attempt within 15 minutes → expect 429
   - Wait 15 minutes, try again → expect success

5. **Root User Flow**:
   - Bootstrap root user via environment variables
   - Verify MustChangePassword flag is true
   - Login as root user
   - Change password
   - Verify MustChangePassword flag is false
   - Verify can access other protected endpoints

**Expected Outcome**: All manual test scenarios pass, confirming correct implementation.

---

### Step 10: Documentation and Cleanup

1. **Update OpenAPI Documentation**:
   - Verify endpoint appears in Swagger UI at `/scalar/v1`
   - Check that request/response models are documented
   - Verify all status codes are listed with examples

2. **Code Review Checklist**:
   - [ ] No passwords logged anywhere
   - [ ] UserIds are logged, not email addresses
   - [ ] Proper error handling with try-catch blocks
   - [ ] Database operations use async/await
   - [ ] Refresh tokens properly revoked
   - [ ] MustChangePassword flag cleared on success
   - [ ] Rate limiting configured and applied
   - [ ] Authorization required on endpoint
   - [ ] FluentValidation rules complete
   - [ ] Integration tests cover all scenarios
   - [ ] TypedResults used (not Results or IResult)
   - [ ] No code warnings (TreatWarningsAsErrors=true)

3. **Run Build Verification**:
   ```bash
   dotnet restore
   dotnet build -c Release -p:TreatWarningsAsErrors=true
   dotnet test
   dotnet format --verify-no-changes
   ```

4. **Git Commit**:
   ```bash
   git add .
   git commit -m "feat: add password change endpoint for user security

   - Implement POST /api/v1/auth/change-password endpoint
   - Add ChangePasswordRequest contract with FluentValidation
   - Extend AuthService with ChangePasswordAsync method
   - Revoke all refresh tokens on password change for security
   - Clear MustChangePassword flag for bootstrapped root users
   - Add rate limiting to prevent brute force attacks (5 per 15 min)
   - Include comprehensive integration tests

   🤖 Generated with [Claude Code](https://claude.com/claude-code)

   Co-Authored-By: Claude <noreply@anthropic.com>"
   ```

**Expected Outcome**: Production-ready implementation with complete documentation and passing tests.

---

## Summary

This implementation plan provides a comprehensive guide for implementing the POST /api/v1/auth/change-password endpoint. The implementation follows all project standards including:

- **ASP.NET Core Minimal APIs** with hierarchical group structure
- **TypedResults** for type-safe responses
- **FluentValidation** with automatic validation via SharpGrip
- **Service layer pattern** with command/result types
- **ASP.NET Identity** for password management
- **Rate limiting** to prevent brute force attacks
- **Comprehensive security measures** including token revocation
- **Complete integration test coverage** using TUnit and WebApplicationFactory

The endpoint ensures security by requiring authentication, verifying the current password, enforcing strong password policies, revoking all existing sessions on password change, and implementing rate limiting to prevent abuse.
