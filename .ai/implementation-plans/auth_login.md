# API Endpoint Implementation Plan: POST /api/v1/auth/login

## 1. Endpoint Overview

This endpoint authenticates users using ASP.NET Identity and returns a JWT access token plus a server-side refresh token. It includes special handling for root users who must change their password on first login by setting the `mustChangePassword` flag in the response.

**Key Characteristics**:
- Uses ASP.NET Core Identity's `SignInManager<ApplicationUser>` for secure password verification
- Validates credentials using constant-time comparison to prevent timing attacks
- Generates short-lived JWT access tokens (15-30 minutes) via `IJwtTokenGenerator`
- Creates sliding refresh tokens stored server-side in PostgreSQL
- Implements account lockout protection after N failed attempts
- Supports "remember me" functionality (extends refresh token lifetime to 30 days)
- Returns `mustChangePassword` flag from user profile for forced password rotation flows
- Uses `TypedResults` for type-safe responses and automatic OpenAPI documentation

## 2. Request Details

- **HTTP Method**: POST
- **URL Structure**: `/api/v1/auth/login`
- **Content-Type**: `application/json`
- **Authentication**: None required (public endpoint)

### Parameters

**Required (all fields in request body)**:
- `email` (string): User's email address (validated for format)
- `password` (string): User's password (verified against stored hash)
- `rememberMe` (bool): Flag to extend refresh token lifetime (30 days if true, 7 days if false)

**Optional**: None

### Request Body Example
```json
{
  "email": "user@example.com",
  "password": "Passw0rd!",
  "rememberMe": true
}
```

## 3. Used Types

### Request/Response Contracts (RSSVibe.Contracts/Auth)
Both contracts already exist and follow project conventions:

```csharp
// Request with nested validator
public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe
)
{
    public sealed class Validator : AbstractValidator<LoginRequest>
    {
        // Validation rules already implemented:
        // - Email: NotEmpty, EmailAddress format
        // - Password: NotEmpty (no complexity check on login)
        // - RememberMe: NotNull
    }
}

// Response
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    bool MustChangePassword
);
```

### Service Layer Types (to be created)

```csharp
// Command model for service layer
public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe
);

// Result type for login operation
public sealed record LoginResult
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresInSeconds { get; init; }
    public bool MustChangePassword { get; init; }
    public LoginError? Error { get; init; }
}

public enum LoginError
{
    None,
    InvalidCredentials,      // Wrong email or password
    AccountLocked,           // Too many failed attempts
    IdentityStoreUnavailable // Database or Identity framework error
}
```

### Entities (to be created)

**RefreshToken entity** for server-side token storage:

```csharp
// RSSVibe.Data/Entities/RefreshToken.cs
public sealed class RefreshToken
{
    public Guid Id { get; set; } // UUIDv7, Primary key
    public Guid UserId { get; set; } // FK to AspNetUsers
    public required string Token { get; set; } // Cryptographically secure random string
    public DateTime ExpiresAt { get; set; } // Expiration timestamp
    public DateTime CreatedAt { get; set; } // Creation timestamp
    public DateTime? RevokedAt { get; set; } // Revocation timestamp (null if active)
    public bool IsUsed { get; set; } // Replay detection flag

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
```

### Existing Dependencies
- `ApplicationUser` in `RSSVibe.Data.Entities` (already exists)
- `IJwtTokenGenerator` in `RSSVibe.Services.Auth` (already implemented)
- `JwtConfiguration` in `RSSVibe.Services.Auth` (already exists)

## 4. Response Details

### Success Response (200 OK)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "Kz8xN2FmZDQtZGY5Yi00YjYxLWI5ZjMtMDk4NzY1NDMyMWFi",
  "expiresInSeconds": 900,
  "mustChangePassword": false
}
```

**Headers**:
- `Content-Type: application/json`

### Error Responses

**400 Bad Request** (Validation Failure)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["Email must be a valid email address"],
    "Password": ["Password is required"]
  }
}
```

**401 Unauthorized** (Invalid Credentials)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Authentication failed",
  "status": 401,
  "detail": "Invalid email or password."
}
```

**423 Locked** (Account Locked)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.12",
  "title": "Account locked",
  "status": 423,
  "detail": "Account has been locked due to multiple failed login attempts. Please try again later or contact support."
}
```

**503 Service Unavailable** (Identity Store Error)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service temporarily unavailable",
  "status": 503,
  "detail": "Unable to connect to the identity store. Please try again later."
}
```

## 5. Data Flow

### High-Level Flow
1. **Request Reception**: Minimal API endpoint receives LoginRequest
2. **Auto-Validation**: SharpGrip.FluentValidation.AutoValidation.Endpoints runs LoginRequest.Validator
3. **Service Layer**: Map to LoginCommand and invoke AuthService.LoginAsync
4. **User Lookup**: Use UserManager to find user by email (normalized)
5. **Credential Check**: Use SignInManager.CheckPasswordSignInAsync (constant-time comparison)
6. **Lockout Check**: SignInManager automatically handles lockout logic
7. **Token Generation**:
   - Generate JWT access token via IJwtTokenGenerator
   - Generate refresh token (cryptographically secure random string)
   - Store refresh token in PostgreSQL with expiration
8. **Response Mapping**: Map LoginResult to LoginResponse
9. **Return Result**: 200 OK with tokens

### Detailed Service Interaction
```
Client Request
    ↓
Minimal API Endpoint (POST /api/v1/auth/login)
    ↓
Auto-Validation (LoginRequest.Validator)
    ↓
IAuthService.LoginAsync(LoginCommand)
    ↓
UserManager<ApplicationUser>.FindByEmailAsync (check existence)
    ↓
SignInManager<ApplicationUser>.CheckPasswordSignInAsync (verify password + lockout)
    ↓ (if successful)
IJwtTokenGenerator.GenerateAccessToken(user)
    ↓
IJwtTokenGenerator.GenerateRefreshToken()
    ↓
ApplicationDbContext.RefreshTokens.Add (store refresh token)
    ↓
ApplicationDbContext.SaveChangesAsync
    ↓
Map to LoginResponse
    ↓
200 OK Response with tokens
```

### Database Interactions
- **Read**: Find user by normalized email (AspNetUsers table)
- **Read**: Check lockout status and failure count (AspNetUsers table)
- **Write**: Insert new refresh token (RefreshTokens table)
- **Write**: Update AccessFailedCount on failure or reset on success (AspNetUsers table, handled by SignInManager)
- **Transaction**: Use transaction for refresh token creation

## 6. Security Considerations

### Authentication & Authorization
- **No authentication required** for login endpoint (public access)
- **No authorization** needed (anyone can attempt login)

### Data Validation & Sanitization
- **Email Validation**:
    - Format validation via FluentValidation
    - Normalization handled by ASP.NET Identity (uppercase invariant)
- **Password Security**:
    - Verified using SignInManager (PBKDF2 hash comparison)
    - Constant-time comparison prevents timing attacks
    - Never stored in plain text or logged
    - Never returned in response
- **RememberMe Flag**:
    - Boolean validation via FluentValidation
    - Affects refresh token expiration (7 days vs 30 days)

### Security Threats & Mitigations

| Threat | Mitigation |
|--------|-----------|
| **Credential Stuffing** | Rate limiting on login endpoint (5 attempts per minute per IP) |
| **Brute Force** | ASP.NET Identity account lockout after 5 failed attempts for 15 minutes |
| **Timing Attacks** | SignInManager uses constant-time password comparison |
| **Enumeration** | Generic 401 error; don't reveal whether email exists |
| **Token Theft** | JWT short-lived (15-30 min), refresh tokens single-use |
| **Replay Attacks** | Mark refresh token as used after exchange; revoke on reuse attempt |
| **Session Fixation** | Generate new refresh token on each login |
| **Password Logging** | Never log password values; use [Redacted] placeholder |
| **SQL Injection** | Mitigated by EF Core parameterized queries |
| **XSS in Tokens** | Tokens are opaque strings; no user-controlled content |

### Token Security
- **Access Token (JWT)**:
    - Short-lived (15-30 minutes)
    - Signed with HMAC-SHA256
    - Contains minimal claims (sub, email, display_name, must_change_password)
    - Validated on every API request via JWT bearer middleware
- **Refresh Token**:
    - Cryptographically secure random string (64 bytes, base64 encoded)
    - Stored server-side in PostgreSQL
    - Single-use (marked as used after exchange)
    - Expires after 7 days (default) or 30 days (remember me)
    - Revoked on logout or suspicious activity

### Account Lockout Policy
- **Lockout after**: 5 failed login attempts
- **Lockout duration**: 15 minutes
- **Configurable in**: `Program.cs` via `IdentityOptions.Lockout`

### Audit & Compliance
- Log all login attempts (success and failure) with anonymized email
- Include timestamp, IP address (future enhancement), and outcome
- Log account lockout events at Warning level
- Store login history via OpenTelemetry traces for security audit

## 7. Error Handling

### Validation Errors (400)
**Trigger**: FluentValidation fails before handler executes

**Scenarios**:
- Empty or invalid email format
- Password empty
- RememberMe null or missing

**Handling**:
- Automatic handling by SharpGrip.FluentValidation.AutoValidation.Endpoints
- Returns RFC 9110 problem details format
- Logs at Debug level (normal operation, not an error)

### Invalid Credentials (401)
**Trigger**: Email not found OR password incorrect

**Scenarios**:
- User does not exist (email not registered)
- Password does not match stored hash
- SignInManager.CheckPasswordSignInAsync returns SignInResult.Failed

**Handling** (using TypedResults):
```csharp
if (signInResult.IsNotAllowed || !signInResult.Succeeded)
{
    // Increment failure count (handled by SignInManager)
    logger.LogInformation(
        "Failed login attempt for email: {Email}",
        AnonymizeEmail(command.Email));

    return TypedResults.Problem(
        title: "Authentication failed",
        detail: "Invalid email or password.",
        statusCode: StatusCodes.Status401Unauthorized
    );
}
```

**Important**: Use same error message for "user not found" and "wrong password" to prevent enumeration

**Logging**: Info level with anonymized email

### Account Locked (423)
**Trigger**: Too many failed login attempts (lockout threshold reached)

**Scenarios**:
- SignInManager.CheckPasswordSignInAsync returns SignInResult.LockedOut
- User account manually locked by admin (future feature)

**Handling** (using TypedResults):
```csharp
if (signInResult.IsLockedOut)
{
    logger.LogWarning(
        "Login attempt for locked account: {Email}",
        AnonymizeEmail(command.Email));

    return TypedResults.Problem(
        title: "Account locked",
        detail: "Account has been locked due to multiple failed login attempts. Please try again later or contact support.",
        statusCode: StatusCodes.Status423Locked
    );
}
```

**Logging**: Warning level with anonymized email

### Identity Store Errors (503)
**Trigger**: Database connection failure or UserManager/SignInManager errors

**Scenarios**:
- PostgreSQL connection timeout
- Database server unreachable
- Transaction deadlock
- Identity framework exceptions during token storage

**Handling** (using TypedResults):
```csharp
try
{
    // ... authentication logic ...
}
catch (DbException ex)
{
    logger.LogError(ex, "Database error during login");
    return TypedResults.Problem(
        title: "Service temporarily unavailable",
        detail: "Unable to connect to the identity store. Please try again later.",
        statusCode: StatusCodes.Status503ServiceUnavailable
    );
}
```

**Logging**: Error level with full exception details

### Error Response Format
All errors follow RFC 9110 Problem Details format (already configured via `builder.Services.AddProblemDetails()`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.x.x",
  "title": "Human-readable summary",
  "status": 401,
  "detail": "Detailed explanation"
}
```

## 8. Performance Considerations

### Potential Bottlenecks
1. **Password Verification**: Intentionally slow (PBKDF2 with 100k iterations)
    - Expected: 50-100ms per request
    - Acceptable for login (infrequent operation)
    - Prevents brute force attacks

2. **Database Round Trips**: Multiple queries (user lookup, refresh token storage)
    - User lookup: ~5-10ms
    - Refresh token insert: ~5-10ms
    - Total: ~60-120ms including password hashing

3. **JWT Generation**: Minimal performance impact (~1-2ms)

4. **Token Storage**: Insert into RefreshTokens table
    - Indexed on Token (unique) and UserId
    - Expected: ~5-10ms

### Optimization Strategies
- **Connection pooling**: Already configured via Npgsql
- **Async/await**: Use throughout to avoid thread pool starvation
- **Token cleanup**: Background job to delete expired refresh tokens (prevents table bloat)
- **Caching**: Do NOT cache user credentials (security risk)
- **Indexes**:
    - Unique index on RefreshTokens.Token for fast lookup
    - Index on RefreshTokens.UserId for user's active tokens query
    - Index on RefreshTokens.ExpiresAt for cleanup job

### Scalability Notes
- **Stateless JWT**: Access tokens are self-contained; no server-side state
- **Refresh token storage**: PostgreSQL handles concurrent inserts efficiently
- **Horizontal scaling**: Endpoint is stateless; scales horizontally without issues
- **Token cleanup**: Scheduled background job (TickerQ) to delete expired tokens

### Monitoring Recommendations
- Track login success rate (target: >95%)
- Monitor average response time (target: <200ms including password hashing)
- Alert on 503 errors (indicates infrastructure problems)
- Dashboard metrics:
    - Logins per hour/day
    - Failed login attempts per hour
    - Account lockouts per day
    - Refresh token usage patterns

## 9. Implementation Steps

### Step 1: Create RefreshToken Entity
**Location**: `src/RSSVibe.Data/Entities/RefreshToken.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace RSSVibe.Data.Entities;

/// <summary>
/// Server-side storage for refresh tokens with expiration and revocation support.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>
    /// Primary key. UUIDv7 generated by application.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to AspNetUsers. User who owns this refresh token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Cryptographically secure random token string (base64 encoded).
    /// Unique index enforced in database.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// Expiration timestamp. Tokens cannot be used after this time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Creation timestamp. Set to UtcNow on creation.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Revocation timestamp. Null if token is still active.
    /// Set when token is used, user logs out, or security breach detected.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Flag indicating token has been used for refresh.
    /// Used to detect replay attacks (token reuse).
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Navigation property to user entity.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;
}
```

**Key Points**:
- Use `Guid.CreateVersion7()` for Id generation (temporal ordering)
- Token field is unique and indexed for fast lookup
- ExpiresAt allows sliding expiration (7 days default, 30 days with rememberMe)
- RevokedAt and IsUsed support revocation and replay detection

### Step 2: Configure RefreshToken Entity
**Location**: `src/RSSVibe.Data/Configurations/RefreshTokenConfiguration.cs`

**Create entity configuration class**:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

/// <summary>
/// Entity Framework configuration for RefreshToken entity.
/// Defines table structure, indexes, and relationships.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        // Primary key configuration
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .ValueGeneratedNever(); // Application generates UUIDv7

        // Token configuration - base64 encoded 64 bytes = ~88 chars, allow headroom
        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(512);

        // Timestamp configurations
        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(rt => rt.RevokedAt)
            .IsRequired(false);

        // IsUsed flag for replay detection
        builder.Property(rt => rt.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        // Index on Token for fast lookup and prevent duplicates
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        // Index on UserId for querying user's active tokens
        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        // Index on ExpiresAt for cleanup job
        builder.HasIndex(rt => rt.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

        // Foreign key relationship to ApplicationUser
        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Add DbSet to RssVibeDbContext** (`src/RSSVibe.Data/RssVibeDbContext.cs`):
```csharp
public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

**Note**: The configuration is automatically discovered and applied via `ApplyConfigurationsFromAssembly()` in `OnModelCreating()`.

### Step 3: Create Database Migration
**Command**:
```bash
cd src/RSSVibe.Data
bash add_migration.sh AddRefreshTokenEntity
```

**Review migration** to ensure:
- Table created: RefreshTokens
- Columns: Id (uuid), UserId (uuid), Token (varchar 512), ExpiresAt (timestamptz), CreatedAt (timestamptz), RevokedAt (nullable timestamptz), IsUsed (boolean)
- Indexes: Token (unique), UserId, ExpiresAt
- Foreign key: UserId -> AspNetUsers(Id) on delete cascade

**Apply migration**:
```bash
dotnet ef database update --project src/RSSVibe.Data --startup-project src/RSSVibe.ApiService
```

### Step 4: Add Login Method to IAuthService Interface
**Location**: `src/RSSVibe.Services/Auth/IAuthService.cs`

```csharp
/// <summary>
/// Authenticates user credentials and generates access and refresh tokens.
/// </summary>
/// <param name="command">Login command containing email, password, and remember me flag.</param>
/// <param name="cancellationToken">Cancellation token for the operation.</param>
/// <returns>Result containing tokens on success or error information on failure.</returns>
Task<LoginResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
```

### Step 5: Create Service Layer Models
**Location**: `src/RSSVibe.Services/Auth/`

**Create LoginCommand.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for user login operation.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe
);
```

**Create LoginResult.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of login operation containing tokens or error information.
/// </summary>
public sealed record LoginResult
{
    /// <summary>
    /// Indicates if login was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// JWT access token. Null if login failed.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Refresh token string. Null if login failed.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Access token expiration time in seconds. Zero if login failed.
    /// </summary>
    public int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Flag indicating user must change password before accessing protected resources.
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Error code if login failed. Null if successful.
    /// </summary>
    public LoginError? Error { get; init; }
}
```

**Create LoginError.cs**:
```csharp
namespace RSSVibe.Services.Auth;

/// <summary>
/// Error codes for login operation failures.
/// </summary>
public enum LoginError
{
    /// <summary>
    /// No error occurred (successful login).
    /// </summary>
    None,

    /// <summary>
    /// Email or password is incorrect.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Account is locked due to too many failed attempts.
    /// </summary>
    AccountLocked,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    IdentityStoreUnavailable
}
```

### Step 6: Implement LoginAsync in AuthService
**Location**: `src/RSSVibe.Services/Auth/AuthService.cs`

**Update primary constructor** to inject additional dependencies:
```csharp
internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,  // NEW
    IJwtTokenGenerator jwtTokenGenerator,          // NEW
    ApplicationDbContext dbContext,                 // NEW
    IOptions<JwtConfiguration> jwtConfig,          // NEW
    ILogger<AuthService> logger) : IAuthService
```

**Add LoginAsync method**:
```csharp
/// <inheritdoc />
public async Task<LoginResult> LoginAsync(
    LoginCommand command,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Find user by email (normalized by Identity)
        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            // Don't reveal that email doesn't exist (prevents enumeration)
            logger.LogInformation(
                "Failed login attempt for non-existent email: {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = false,
                Error = LoginError.InvalidCredentials
            };
        }

        // Check password and lockout status using SignInManager
        // This handles:
        // - Password verification (constant-time comparison)
        // - Account lockout logic (increment failure count, check lockout)
        // - Two-factor authentication (if enabled in future)
        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            command.Password,
            lockoutOnFailure: true); // Enable lockout on failed attempts

        if (signInResult.IsLockedOut)
        {
            logger.LogWarning(
                "Login attempt for locked account: {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = false,
                Error = LoginError.AccountLocked
            };
        }

        if (!signInResult.Succeeded)
        {
            // Password is incorrect or account is not allowed
            logger.LogInformation(
                "Failed login attempt for email: {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = false,
                Error = LoginError.InvalidCredentials
            };
        }

        // Login successful - generate tokens

        // 1. Generate JWT access token
        var (accessToken, expiresInSeconds) = jwtTokenGenerator.GenerateAccessToken(user);

        // 2. Generate refresh token
        var refreshTokenString = jwtTokenGenerator.GenerateRefreshToken();

        // 3. Calculate expiration based on rememberMe flag
        var config = jwtConfig.Value;
        var refreshTokenExpiration = command.RememberMe
            ? DateTime.UtcNow.AddDays(config.RefreshTokenExpirationDays)
            : DateTime.UtcNow.AddDays(7); // Default 7 days without rememberMe

        // 4. Store refresh token in database
        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Token = refreshTokenString,
            ExpiresAt = refreshTokenExpiration,
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. Reset failure count on successful login
        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation(
            "Successful login for user: {Email}",
            AnonymizeEmail(command.Email));

        return new LoginResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresInSeconds = expiresInSeconds,
            MustChangePassword = user.MustChangePassword
        };
    }
    catch (DbException ex)
    {
        logger.LogError(
            ex,
            "Database error during login for {Email}",
            AnonymizeEmail(command.Email));

        return new LoginResult
        {
            Success = false,
            Error = LoginError.IdentityStoreUnavailable
        };
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Unexpected error during login for {Email}",
            AnonymizeEmail(command.Email));

        return new LoginResult
        {
            Success = false,
            Error = LoginError.IdentityStoreUnavailable
        };
    }
}
```

**Key Implementation Details**:
- Use `SignInManager.CheckPasswordSignInAsync` instead of manual password verification
- Pass `lockoutOnFailure: true` to enable automatic lockout logic
- Reset failure count on successful login via `ResetAccessFailedCountAsync`
- Calculate refresh token expiration based on `rememberMe` flag
- Use `Guid.CreateVersion7()` for refresh token ID
- Store refresh token in database within same transaction
- Return `mustChangePassword` from user profile

### Step 7: Update Service Registration
**Location**: `src/RSSVibe.Services/Extensions/ServiceCollectionExtensions.cs`

**Add dependencies** (if not already present):
```csharp
public static IServiceCollection AddRssVibeServices(this IServiceCollection services)
{
    // Register Auth services
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

    // ApplicationDbContext is already registered in Program.cs via AddDbContext
    // SignInManager is already registered by AddIdentity

    return services;
}
```

### Step 8: Configure Identity Lockout Policy
**Location**: `src/RSSVibe.ApiService/Program.cs`

**Update Identity configuration**:
```csharp
// Configure Identity options
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
});
```

### Step 9: Configure JWT Settings
**Location**: `src/RSSVibe.ApiService/appsettings.json`

**Add JWT configuration**:
```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-32-characters-long-for-hs256",
    "Issuer": "https://localhost:5001",
    "Audience": "rssvibe-web",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30
  }
}
```

**For production** (`appsettings.Production.json`):
```json
{
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 30
  }
}
```

**Register configuration in Program.cs**:
```csharp
builder.Services.Configure<JwtConfiguration>(
    builder.Configuration.GetSection("Jwt"));
```

### Step 10: Create Minimal API Endpoint
**Location**: `src/RSSVibe.ApiService/Endpoints/Auth/LoginEndpoint.cs`

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for user authentication (login).
/// </summary>
public static class LoginEndpoint
{
    /// <summary>
    /// Maps the login endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapLoginEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Authenticate user credentials";
                operation.Description = "Authenticates user with email and password. " +
                    "Returns JWT access token and refresh token for subsequent API calls. " +
                    "Supports 'remember me' to extend refresh token lifetime to 30 days.";
                return operation;
            })
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<
        Ok<LoginResponse>,
        ProblemHttpResult>>
        HandleAsync(
            LoginRequest request,
            IAuthService authService,
            ILogger<LoginEndpoint> logger,
            CancellationToken cancellationToken)
    {
        // Map request to command
        var command = new LoginCommand(
            request.Email,
            request.Password,
            request.RememberMe);

        // Call service
        var result = await authService.LoginAsync(command, cancellationToken);

        // Handle result
        if (!result.Success)
        {
            return result.Error switch
            {
                LoginError.InvalidCredentials => TypedResults.Problem(
                    title: "Authentication failed",
                    detail: "Invalid email or password.",
                    statusCode: StatusCodes.Status401Unauthorized),

                LoginError.AccountLocked => TypedResults.Problem(
                    title: "Account locked",
                    detail: "Account has been locked due to multiple failed login attempts. Please try again later or contact support.",
                    statusCode: StatusCodes.Status423Locked),

                LoginError.IdentityStoreUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to connect to the identity store. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Login failed",
                    detail: "An unexpected error occurred during login.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // Success - return 200 OK with tokens
        var response = new LoginResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresInSeconds,
            result.MustChangePassword);

        logger.LogInformation(
            "User logged in successfully: {Email}",
            AnonymizeEmail(request.Email));

        return TypedResults.Ok(response);
    }

    private static string AnonymizeEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "[null]";
        }

        var parts = email.Split('@');
        if (parts.Length != 2)
        {
            return "[invalid]";
        }

        return $"{parts[0][0]}***@{parts[1]}";
    }
}
```

**Key Points**:
- Use `TypedResults` for all return statements
- Explicit return type uses `Results<Ok<LoginResponse>, ProblemHttpResult>` union type
- Return type documents all possible responses: 200, 401, 423, 503
- Map service errors to appropriate HTTP status codes
- Anonymize email in logs

### Step 11: Register LoginEndpoint in AuthGroup
**Location**: `src/RSSVibe.ApiService/Endpoints/Auth/AuthGroup.cs`

**Add login endpoint registration**:
```csharp
public static class AuthGroup
{
    public static IEndpointRouteBuilder MapAuthGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth")
            .WithTags("Auth");

        // Register all auth endpoints
        group.MapRegisterEndpoint();
        group.MapLoginEndpoint(); // NEW

        return endpoints;
    }
}
```

### Step 12: Ensure Dependencies are Registered in Program.cs
**Location**: `src/RSSVibe.ApiService/Program.cs`

**Verify registrations** (should already exist from register endpoint):
```csharp
// Add Identity with SignInManager
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<SignInManager<ApplicationUser>>()
    .AddDefaultTokenProviders();

// Configure Identity options (password, lockout)
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
});

// Configure JWT
builder.Services.Configure<JwtConfiguration>(
    builder.Configuration.GetSection("Jwt"));

// Register services
builder.Services.AddRssVibeServices();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequest.Validator>();
```

### Step 13: Add Integration Tests
**Location**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Auth/LoginEndpointTests.cs`

**Test cases to implement**:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using RSSVibe.Contracts.Auth;
using TUnit.Assertions.Extensions;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

[TestFixture]
public class LoginEndpointTests(WebApplicationFactory<Program> factory) : TestsBase(factory)
{
    [Test]
    public async Task LoginEndpoint_WithValidCredentials_ShouldReturn200WithTokens()
    {
        // Arrange
        var client = CreateClient();

        // First register a user
        var registerRequest = new RegisterRequest(
            Email: "test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Now login
        var loginRequest = new LoginRequest(
            Email: "test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse!.AccessToken).IsNotEmpty();
        await Assert.That(loginResponse.RefreshToken).IsNotEmpty();
        await Assert.That(loginResponse.ExpiresInSeconds).IsGreaterThan(0);
        await Assert.That(loginResponse.MustChangePassword).IsEqualTo(false);
    }

    [Test]
    public async Task LoginEndpoint_WithInvalidEmail_ShouldReturn400()
    {
        // Arrange
        var client = CreateClient();
        var loginRequest = new LoginRequest(
            Email: "invalid-email",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task LoginEndpoint_WithMissingPassword_ShouldReturn400()
    {
        // Arrange
        var client = CreateClient();
        var loginRequest = new LoginRequest(
            Email: "test@rssvibe.local",
            Password: "",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task LoginEndpoint_WithNonExistentUser_ShouldReturn401()
    {
        // Arrange
        var client = CreateClient();
        var loginRequest = new LoginRequest(
            Email: "nonexistent@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails!.Detail).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginEndpoint_WithWrongPassword_ShouldReturn401()
    {
        // Arrange
        var client = CreateClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "test2@rssvibe.local",
            Password: "CorrectPassword123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Try to login with wrong password
        var loginRequest = new LoginRequest(
            Email: "test2@rssvibe.local",
            Password: "WrongPassword123!",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task LoginEndpoint_WithLockedAccount_ShouldReturn423()
    {
        // Arrange
        var client = CreateClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "locked@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Locked User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Make 5 failed login attempts to trigger lockout
        var wrongLoginRequest = new LoginRequest(
            Email: "locked@rssvibe.local",
            Password: "WrongPassword123!",
            RememberMe: false
        );

        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", wrongLoginRequest);
        }

        // Now try with correct password (should be locked)
        var correctLoginRequest = new LoginRequest(
            Email: "locked@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", correctLoginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Locked);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails!.Detail).Contains("locked");
    }

    [Test]
    public async Task LoginEndpoint_WithMustChangePassword_ShouldReturnTrueFlag()
    {
        // Arrange
        var client = CreateClient();

        // Register user with mustChangePassword flag
        var registerRequest = new RegisterRequest(
            Email: "mustchange@rssvibe.local",
            Password: "TemporaryPassword123!",
            DisplayName: "Must Change User",
            MustChangePassword: true
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Login
        var loginRequest = new LoginRequest(
            Email: "mustchange@rssvibe.local",
            Password: "TemporaryPassword123!",
            RememberMe: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse!.MustChangePassword).IsEqualTo(true);
    }

    [Test]
    public async Task LoginEndpoint_WithRememberMe_ShouldCreateLongerRefreshToken()
    {
        // Arrange
        var client = CreateClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "rememberme@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Remember Me User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Login with rememberMe = true
        var loginRequest = new LoginRequest(
            Email: "rememberme@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse!.RefreshToken).IsNotEmpty();

        // Verify refresh token exists in database with extended expiration
        // (would need to query database directly or use a helper method)
    }
}
```

**Test Infrastructure Notes**:
- Use `WebApplicationFactory<Program>` for in-memory hosting
- Use Testcontainers for PostgreSQL instance (already configured)
- Each test should create unique users to avoid conflicts
- Use `CreateClient()` helper from `TestsBase`

### Step 14: Add Unit Tests for AuthService.LoginAsync
**Location**: `Tests/RSSVibe.Services.Tests/Auth/AuthServiceLoginTests.cs`

**Test cases to implement**:
- `LoginAsync_WithValidCredentials_ShouldReturnSuccessWithTokens`
- `LoginAsync_WithNonExistentEmail_ShouldReturnInvalidCredentialsError`
- `LoginAsync_WithWrongPassword_ShouldReturnInvalidCredentialsError`
- `LoginAsync_WithLockedAccount_ShouldReturnAccountLockedError`
- `LoginAsync_WhenDatabaseThrows_ShouldReturnIdentityStoreUnavailableError`
- `LoginAsync_WithRememberMe_ShouldCreateLongerExpirationToken`
- `LoginAsync_SuccessfulLogin_ShouldResetFailureCount`

**Mocking strategy**:
- Mock `UserManager<ApplicationUser>` (use NSubstitute)
- Mock `SignInManager<ApplicationUser>` (use NSubstitute)
- Mock `IJwtTokenGenerator`
- Mock `ApplicationDbContext` or use in-memory database
- Mock `ILogger<AuthService>`
- Prefer real `ApplicationUser` and `RefreshToken` instances

### Step 15: Add OpenTelemetry Tracing (Future Enhancement)
**Location**: `src/RSSVibe.ApiService/Program.cs`

**Add tracing for auth events** (optional, for MVP+):
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("RSSVibe.Auth");
        // ... other sources ...
    });
```

**In AuthService**:
```csharp
using System.Diagnostics;

private static readonly ActivitySource ActivitySource = new("RSSVibe.Auth");

public async Task<LoginResult> LoginAsync(...)
{
    using var activity = ActivitySource.StartActivity("AuthService.Login");
    activity?.SetTag("auth.email", AnonymizeEmail(command.Email));

    // ... login logic ...

    activity?.SetTag("auth.success", result.Success);
    return result;
}
```

### Step 16: Add Refresh Token Cleanup Job (Future Enhancement)
**Location**: `src/RSSVibe.ApiService/BackgroundJobs/RefreshTokenCleanupJob.cs`

**Purpose**: Delete expired refresh tokens to prevent database bloat

**Implementation**:
```csharp
public class RefreshTokenCleanupJob : IHostedService
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Delete expired tokens older than 30 days
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        await dbContext.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoffDate || rt.RevokedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
```

**Schedule**: Run daily via TickerQ or ASP.NET Core BackgroundService

### Step 17: Manual Testing Checklist

**Test scenarios**:
1. ✓ Successful login with valid credentials
2. ✓ Validation errors (invalid email, missing password)
3. ✓ Login with non-existent email (should return 401, not reveal existence)
4. ✓ Login with wrong password (should return 401)
5. ✓ Account lockout after 5 failed attempts (should return 423)
6. ✓ Login after lockout expires (should succeed after 15 minutes)
7. ✓ RememberMe flag (verify refresh token expiration in database)
8. ✓ MustChangePassword flag (verify flag in response)
9. ✓ JWT token validation (decode token and verify claims)
10. ✓ Refresh token stored in database (query RefreshTokens table)
11. ✓ Failed login increments AccessFailedCount (verify in database)
12. ✓ Successful login resets AccessFailedCount to 0

**Tools**:
- curl, Postman, or HTTP client
- PostgreSQL client (pgAdmin, psql, DataGrip)
- JWT decoder (jwt.io)

**Example curl commands**:
```bash
# Successful login
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@rssvibe.local",
    "password": "ValidPassword123!",
    "rememberMe": true
  }'

# Invalid credentials
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@rssvibe.local",
    "password": "WrongPassword!",
    "rememberMe": false
  }'

# Validation error
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalid-email",
    "password": "ValidPassword123!",
    "rememberMe": false
  }'
```

### Step 18: Security Review Checklist

**Before deployment**:
- [ ] Passwords never logged (use AnonymizeEmail helper)
- [ ] Generic error messages for invalid credentials (no email enumeration)
- [ ] Account lockout enabled (5 attempts, 15 min lockout)
- [ ] Constant-time password comparison (via SignInManager)
- [ ] HTTPS enforced in production (configured elsewhere)
- [ ] JWT secret key is strong (at least 32 characters, from environment variable)
- [ ] Refresh tokens stored server-side (not in JWT)
- [ ] Refresh tokens have expiration (7 days default, 30 days with rememberMe)
- [ ] Refresh tokens are single-use (marked as used after exchange)
- [ ] Rate limiting configured (future enhancement)
- [ ] CORS properly configured for production
- [ ] SQL injection prevented (EF Core parameterized queries)

### Step 19: Performance Testing (Optional)

**Load testing scenarios**:
1. Concurrent login requests (100 users/second)
2. Failed login attempts (measure lockout performance)
3. Database query performance (user lookup, token storage)
4. JWT generation performance
5. Password hashing performance (should be ~50-100ms)

**Tools**: k6, Apache JMeter, or custom load test

**Acceptance criteria**:
- P95 response time < 200ms (excluding password hashing)
- P99 response time < 500ms
- No database connection pool exhaustion
- No thread pool starvation

### Step 20: Documentation Updates

**Update**:
1. API documentation (Swagger/OpenAPI) - automatic via TypedResults
2. README with login flow diagram
3. Security documentation with threat model
4. Deployment guide (JWT secret key configuration)
5. Database migration guide (RefreshToken table)
6. Architecture decision record (ADR) for refresh token strategy

---

## Summary Checklist

### Database Changes
- [ ] RefreshToken entity created in `src/RSSVibe.Data/Entities/`
- [ ] RefreshToken configured in ApplicationDbContext
- [ ] Migration created for RefreshTokens table
- [ ] Migration reviewed (table, columns, indexes, FK)
- [ ] Migration applied to development database
- [ ] Indexes verified (Token unique, UserId, ExpiresAt)

### Service Layer
- [ ] LoginCommand created (public sealed record)
- [ ] LoginResult created (public sealed record)
- [ ] LoginError enum created (public enum)
- [ ] LoginAsync method added to IAuthService interface
- [ ] LoginAsync implemented in AuthService (internal sealed)
- [ ] SignInManager injected into AuthService
- [ ] IJwtTokenGenerator injected into AuthService
- [ ] ApplicationDbContext injected into AuthService
- [ ] JwtConfiguration injected into AuthService

### API Layer
- [ ] LoginEndpoint minimal API created in `src/RSSVibe.ApiService/Endpoints/Auth/`
- [ ] Endpoint uses TypedResults with explicit return type
- [ ] LoginEndpoint registered in AuthGroup
- [ ] Error handling comprehensive (400, 401, 423, 503)
- [ ] OpenAPI documentation added (summary, description)

### Configuration
- [ ] JwtConfiguration section added to appsettings.json
- [ ] JwtConfiguration registered in Program.cs
- [ ] Identity lockout policy configured (5 attempts, 15 min)
- [ ] Identity password policy configured (already done for register)
- [ ] SignInManager registered (already done by AddIdentity)

### Testing
- [ ] Integration tests implemented (8 test cases)
- [ ] Unit tests implemented for AuthService.LoginAsync (7 test cases)
- [ ] Test infrastructure uses Testcontainers for PostgreSQL
- [ ] Manual testing completed (12 scenarios)
- [ ] All tests passing

### Security
- [ ] Passwords never logged (AnonymizeEmail used)
- [ ] Generic error messages (no enumeration)
- [ ] Account lockout enabled and tested
- [ ] Constant-time password comparison (SignInManager)
- [ ] JWT secret key strong and from environment variable
- [ ] Refresh tokens stored server-side
- [ ] Refresh tokens single-use (marked as used)
- [ ] HTTPS enforced in production
- [ ] Security review completed

### Quality Assurance
- [ ] Code follows project conventions (AGENTS.md)
- [ ] Service implementation is `internal sealed`
- [ ] Interface and models are `public`
- [ ] TypedResults used for all endpoint returns
- [ ] Logging implemented throughout
- [ ] Error handling comprehensive
- [ ] Performance acceptable (<200ms P95)
- [ ] Documentation updated

### Deployment Readiness
- [ ] Build succeeds with no warnings
- [ ] All tests pass (unit + integration)
- [ ] Configuration validated for all environments
- [ ] Security checklist completed
- [ ] Database migration plan documented
- [ ] Rollback plan prepared

---

## Future Enhancements (Post-MVP)

1. **Rate Limiting**: Add ASP.NET Core rate limiting middleware
   - 5 login attempts per minute per IP address
   - Return 429 Too Many Requests

2. **IP Address Tracking**: Log IP addresses for security audit
   - Store in login history table
   - Alert on login from new location

3. **Two-Factor Authentication (2FA)**: Add support via ASP.NET Identity
   - SMS or authenticator app
   - Required for admin accounts

4. **Device Fingerprinting**: Track devices for security alerts
   - Notify user of new device login
   - Option to revoke device tokens

5. **Refresh Token Rotation**: Implement automatic rotation
   - Issue new refresh token on each use
   - Revoke old token immediately

6. **Token Blacklist**: Implement JWT blacklist for logout
   - Store revoked JWTs in Redis with expiration
   - Check blacklist on every authenticated request

7. **Password Reset Flow**: Add forgot password endpoint
   - Email verification
   - Time-limited reset token

8. **Session Management**: Add endpoint to list active sessions
   - View all active refresh tokens
   - Revoke individual sessions

9. **Audit Logging**: Enhanced security audit trail
   - Log all login attempts with metadata
   - Store in dedicated audit table
   - Export to SIEM system

10. **Account Recovery**: Add account recovery mechanism
    - Security questions
    - Backup email
    - Support contact

---

## Appendix: Error Code Reference

| Status Code | Error | Description | User Action |
|-------------|-------|-------------|-------------|
| 200 | Success | Login successful | None |
| 400 | Validation | Invalid email format or missing password | Fix input and retry |
| 401 | InvalidCredentials | Email or password incorrect | Verify credentials |
| 423 | AccountLocked | Too many failed attempts | Wait 15 minutes or contact support |
| 503 | IdentityStoreUnavailable | Database or Identity error | Wait and retry, contact support if persistent |

---

## Appendix: Database Schema

### RefreshTokens Table

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | uuid | Primary key | UUIDv7 generated by application |
| UserId | uuid | FK to AspNetUsers(Id), NOT NULL | Owner of the refresh token |
| Token | varchar(512) | NOT NULL, Unique | Base64 encoded random string |
| ExpiresAt | timestamptz | NOT NULL | Expiration timestamp |
| CreatedAt | timestamptz | NOT NULL, default now() | Creation timestamp |
| RevokedAt | timestamptz | Nullable | Revocation timestamp |
| IsUsed | boolean | NOT NULL, default false | Replay detection flag |

### Indexes

- **IX_RefreshTokens_Token** (Unique): Fast lookup by token string
- **IX_RefreshTokens_UserId**: Query user's active tokens
- **IX_RefreshTokens_ExpiresAt**: Cleanup job performance

### Foreign Keys

- **FK_RefreshTokens_AspNetUsers_UserId**: UserId -> AspNetUsers(Id) ON DELETE CASCADE
