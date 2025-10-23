# API Endpoint Implementation Plan: POST /api/v1/auth/register

## 1. Endpoint Overview

This endpoint creates a new user account using ASP.NET Identity. It is designed primarily for local development and initial setup. In production environments where a root user is provisioned via environment variables, this endpoint should be disabled to prevent unauthorized account creation.

**Key Characteristics**:
- Uses ASP.NET Core Identity's `UserManager<ApplicationUser>` for secure user creation
- Validates email uniqueness and password complexity
- Returns 201 Created with Location header on success
- Supports conditional disabling based on environment configuration
- Uses `TypedResults` for type-safe responses and automatic OpenAPI documentation

## 2. Request Details

- **HTTP Method**: POST
- **URL Structure**: `/api/v1/auth/register`
- **Content-Type**: `application/json`

### Parameters

**Required (all fields in request body)**:
- `email` (string): User's email address (validated for format and uniqueness)
- `password` (string): User's password (minimum 12 characters with complexity requirements)
- `displayName` (string): User's display name for UI purposes
- `mustChangePassword` (bool): Flag indicating if user must change password on first login

**Optional**: None

### Request Body Example
```json
{
  "email": "user@example.com",
  "password": "Passw0rd!",
  "displayName": "Jane Doe",
  "mustChangePassword": false
}
```

## 3. Used Types

### Request/Response Contracts (RSSVibe.Contracts/Auth)
Both contracts already exist and follow project conventions:

```csharp
// Request with nested validator
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    bool MustChangePassword
)
{
    public sealed class Validator : AbstractValidator<RegisterRequest>
    {
        // Validation rules already implemented
    }
}

// Response
public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool MustChangePassword
);
```

### Service Layer Types (to be created)

```csharp
// Command model for service layer
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    bool MustChangePassword
);

// Result type for registration operation
public sealed record RegisterUserResult
{
    public Guid UserId { get; init; }
    public string Email { get; init; }
    public string DisplayName { get; init; }
    public bool MustChangePassword { get; init; }
    public bool Success { get; init; }
    public RegistrationError? Error { get; init; }
}

public enum RegistrationError
{
    None,
    EmailAlreadyExists,
    InvalidPassword,
    IdentityStoreUnavailable
}
```

### Entities (already exists)
- `ApplicationUser` in `RSSVibe.Data.Entities` (inherits from `IdentityUser<Guid>`)

## 4. Response Details

### Success Response (201 Created)
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "displayName": "Jane Doe",
  "mustChangePassword": false
}
```

**Headers**:
- `Location: /api/v1/auth/profile`
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
    "Password": ["Password must be at least 12 characters"]
  }
}
```

**409 Conflict** (Email Already Registered)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Email address is already registered",
  "status": 409,
  "detail": "An account with this email address already exists."
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

**403 Forbidden** (Endpoint Disabled in Production)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Registration is disabled",
  "status": 403,
  "detail": "User registration is not available in this environment."
}
```

## 5. Data Flow

### High-Level Flow
1. **Request Reception**: Minimal API endpoint receives RegisterRequest
2. **Auto-Validation**: SharpGrip.FluentValidation.AutoValidation.Endpoints runs RegisterRequest.Validator
3. **Environment Check**: Verify registration is allowed (not production with root user provisioning)
4. **Service Layer**: Map to RegisterUserCommand and invoke AuthService
5. **Identity Check**: Use UserManager to check if email exists (normalized)
6. **User Creation**: Create ApplicationUser entity via UserManager.CreateAsync
7. **Password Storage**: UserManager handles password hashing automatically
8. **Response Mapping**: Map RegisterUserResult to RegisterResponse
9. **Return Result**: 201 Created with Location header

### Detailed Service Interaction
```
Client Request
    ↓
Minimal API Endpoint (POST /api/v1/auth/register)
    ↓
Auto-Validation (RegisterRequest.Validator)
    ↓
Environment Configuration Check
    ↓
IAuthService.RegisterUserAsync(RegisterUserCommand)
    ↓
UserManager<ApplicationUser>.FindByEmailAsync (check existence)
    ↓
UserManager<ApplicationUser>.CreateAsync (create with password)
    ↓
Map to RegisterResponse with Location header
    ↓
201 Created Response
```

### Database Interactions
- **Read**: Check if user exists by normalized email (AspNetUsers table)
- **Write**: Insert new user record (AspNetUsers table)
- **Transaction**: UserManager handles transaction automatically

## 6. Security Considerations

### Authentication & Authorization
- **No authentication required** for registration endpoint (public access)
- **No authorization** needed (anyone can register in allowed environments)

### Data Validation & Sanitization
- **Email Validation**:
  - Format validation via FluentValidation
  - Normalization handled by ASP.NET Identity (uppercase invariant)
  - Uniqueness check via UserManager
- **Password Security**:
  - Minimum 12 characters
  - Complexity: uppercase, lowercase, digit, special character
  - Hashed using ASP.NET Identity's password hasher (PBKDF2)
  - Never stored in plain text or logged
- **Display Name**:
  - Required field, no special sanitization needed (stored as-is)
  - Consider max length constraint (future enhancement)

### Security Threats & Mitigations

| Threat | Mitigation |
|--------|-----------|
| **Enumeration Attack** | Use generic 409 error message; don't reveal whether email exists during validation phase |
| **Weak Passwords** | Enforce strong password policy via validation rules |
| **SQL Injection** | Mitigated by EF Core parameterized queries |
| **Mass Registration** | Consider rate limiting (not in MVP spec) |
| **Production Bypass** | Environment check as first guard; fail fast if disabled |
| **Password Logging** | Never log password values; use [Redacted] in logs |
| **Timing Attacks** | UserManager uses constant-time comparison for passwords |

### Environment-Based Security
- **Development**: Registration enabled by default
- **Production**: Disabled if `RSSVIBE_ROOT_USER_EMAIL` environment variable is set
- Check configuration early in request pipeline

### Audit & Compliance
- Log all registration attempts (success and failure)
- Include timestamp, email (but not password), and outcome
- Consider future enhancement: email verification before account activation

## 7. Error Handling

### Validation Errors (400)
**Trigger**: FluentValidation fails before handler executes

**Scenarios**:
- Empty or invalid email format
- Password too short or missing complexity requirements
- Display name empty
- MustChangePassword null

**Handling**:
- Automatic handling by SharpGrip.FluentValidation.AutoValidation.Endpoints
- Returns RFC 9110 problem details format
- Logs at Debug level (normal operation, not an error)

### Email Conflict (409)
**Trigger**: Email already registered

**Scenarios**:
- Exact email match (case-insensitive)
- Normalized email collision

**Handling** (using TypedResults):
```csharp
if (existingUser is not null)
{
    return TypedResults.Problem(
        title: "Email address is already registered",
        detail: "An account with this email address already exists.",
        statusCode: StatusCodes.Status409Conflict
    );
}
```

**Logging**: Info level with anonymized email (e.g., "u***@example.com")

### Identity Store Errors (503)
**Trigger**: Database connection failure or UserManager errors

**Scenarios**:
- PostgreSQL connection timeout
- Database server unreachable
- Transaction deadlock
- Identity framework exceptions

**Handling** (using TypedResults):
```csharp
try
{
    var result = await _userManager.CreateAsync(user, command.Password);
    if (!result.Succeeded)
    {
        // Handle identity-specific errors
    }
}
catch (DbException ex)
{
    _logger.LogError(ex, "Database error during user registration");
    return TypedResults.Problem(
        title: "Service temporarily unavailable",
        detail: "Unable to connect to the identity store.",
        statusCode: StatusCodes.Status503ServiceUnavailable
    );
}
```

**Logging**: Error level with full exception details

### Production Registration Disabled (403)
**Trigger**: Environment configuration blocks registration

**Scenarios**:
- Production environment with root user provisioning enabled
- Configuration flag explicitly disables registration

**Handling** (using TypedResults):
```csharp
if (!_authConfig.AllowRegistration)
{
    return TypedResults.Problem(
        title: "Registration is disabled",
        detail: "User registration is not available in this environment.",
        statusCode: StatusCodes.Status403Forbidden
    );
}
```

**Logging**: Warning level (attempted access to disabled feature)

### Error Response Format
All errors follow RFC 9110 Problem Details format (already configured via `builder.Services.AddProblemDetails()`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.x.x",
  "title": "Human-readable summary",
  "status": 400,
  "detail": "Detailed explanation",
  "errors": { /* validation errors only */ }
}
```

## 8. Performance Considerations

### Potential Bottlenecks
1. **Password Hashing**: Intentionally slow (PBKDF2 with 100k iterations)
   - Expected: 50-100ms per request
   - Acceptable for registration (infrequent operation)

2. **Database Round Trips**: Two queries (check existence + insert)
   - Optimizable by trying insert and catching unique constraint violation
   - Current approach preferred for better error messages

3. **Email Normalization**: Done by ASP.NET Identity
   - Minimal performance impact

### Optimization Strategies
- **No caching needed**: Registration is write-heavy, not read-heavy
- **Connection pooling**: Already configured via Npgsql
- **Async/await**: Use throughout to avoid thread pool starvation
- **Transaction scope**: UserManager handles automatically; don't wrap in additional scope

### Scalability Notes
- **Low frequency operation**: Registration happens once per user
- **No locking needed**: Unique constraints prevent duplicate emails
- **Horizontal scaling**: Stateless endpoint; scales horizontally without issues

### Monitoring Recommendations
- Track registration success rate
- Monitor average response time (target: <200ms excluding password hashing)
- Alert on 503 errors (indicates infrastructure problems)
- Dashboard metric: registrations per hour/day

## 9. Implementation Steps

### Step 1: Create Service Layer Infrastructure
**Location**: `src/RSSVibe.Services/Auth/`

**Files to create**:
- `IAuthService.cs` (public interface)
- `AuthService.cs` (internal sealed implementation with primary constructor)
- `RegisterUserCommand.cs` (public command model)
- `RegisterUserResult.cs` (public result type)
- `RegistrationError.cs` (public enum for error types)

**Visibility rules**:
- Interface: `public interface IAuthService`
- Implementation: `internal sealed class AuthService`
- Models (Command/Result/Error): `public sealed record` / `public enum`

**Key responsibilities**:
- Encapsulate UserManager interactions
- Handle email existence checking
- Manage user creation with password
- Map between domain models and DTOs
- Return strongly-typed results with success/error status

### Step 2: Create Configuration Model
**Location**: `src/RSSVibe.ApiService/Configuration/`

**File**: `AuthConfiguration.cs`

**Properties**:
```csharp
public sealed class AuthConfiguration
{
    public bool AllowRegistration { get; init; }
    public string? RootUserEmail { get; init; }
    public string? RootUserPassword { get; init; }
}
```

**Binding**: Bind to `appsettings.json` section "Auth"

### Step 3: Create Service Registration Extension Method
**Location**: `src/RSSVibe.Services/Extensions/ServiceCollectionExtensions.cs`

**Create**:
```csharp
using Microsoft.Extensions.DependencyInjection;

namespace RSSVibe.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRssVibeServices(this IServiceCollection services)
    {
        // Register Auth services
        services.AddScoped<IAuthService, AuthService>();

        // Future services will be added here
        // services.AddScoped<IFeedService, FeedService>();

        return services;
    }
}
```

**Key points**:
- Extension method is `public static`
- Returns `IServiceCollection` for method chaining
- Registers `internal sealed` implementations with `public` interfaces
- Single method to register all services from this project

### Step 4: Add Project References
**Location**: `src/RSSVibe.ApiService/RSSVibe.ApiService.csproj`

**Add**:
```xml
<ItemGroup>
  <ProjectReference Include="..\RSSVibe.Services\RSSVibe.Services.csproj" />
</ItemGroup>
```

**Note**: RSSVibe.Services needs references to:
- `RSSVibe.Data` (for ApplicationUser entity and UserManager)
- `Microsoft.Extensions.Identity.Core` (for UserManager)
- `Microsoft.Extensions.Logging.Abstractions` (for ILogger)
- `Microsoft.Extensions.DependencyInjection.Abstractions` (for IServiceCollection)

### Step 5: Configure Services in Program.cs
**Location**: `src/RSSVibe.ApiService/Program.cs`

**Changes**:
```csharp
using RSSVibe.Services.Extensions;

// ... existing code ...

// Add configuration
builder.Services.Configure<AuthConfiguration>(
    builder.Configuration.GetSection("Auth"));

// Add FluentValidation auto-validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequest.Validator>();

// Register all services from RSSVibe.Services via extension method
builder.Services.AddRssVibeServices();

// Configure Identity password requirements
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
});
```

**Key points**:
- Import `RSSVibe.Services.Extensions` namespace
- Call `AddRssVibeServices()` instead of manually registering each service
- Clean, single-line registration for all services from the project

### Step 6: Create Minimal API Endpoint
**Location**: `src/RSSVibe.ApiService/Endpoints/Auth/RegisterEndpoint.cs`

**Structure** (using TypedResults):
```csharp
public static class RegisterEndpoint
{
    public static IEndpointRouteBuilder MapRegisterEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/register", HandleAsync)
            .WithName("Register")
            .WithTags("Auth");

        return endpoints;
    }

    private static async Task<Results<
        Created<RegisterResponse>,
        ValidationProblem,
        ProblemHttpResult,
        ForbidHttpResult>>
        HandleAsync(
            RegisterRequest request,
            IAuthService authService,
            IOptions<AuthConfiguration> config,
            ILogger<RegisterEndpoint> logger,
            CancellationToken cancellationToken)
    {
        // Implementation here
    }
}
```

**Key Points**:
- Use `TypedResults` for all return statements (NOT `Results`)
- Explicit return type uses `Results<T1, T2, T3...>` union type
- No need for `.Produces<>()` calls - return type provides OpenAPI documentation
- Return type documents all possible responses: 201, 400, 409, 503, 403

### Step 7: Implement Endpoint Handler Logic
**Key operations**:
1. Check if registration is allowed (environment configuration)
2. Map RegisterRequest to RegisterUserCommand
3. Call `authService.RegisterUserAsync(command)`
4. Handle result cases using TypedResults:
   - Success: Return `TypedResults.Created()` with Location header
   - EmailAlreadyExists: Return `TypedResults.Problem()` with 409 status
   - InvalidPassword: Return `TypedResults.Problem()` with 400 status
   - IdentityStoreUnavailable: Return `TypedResults.Problem()` with 503 status
   - Registration disabled: Return `TypedResults.Forbid()`
5. Log outcomes appropriately

**Example implementation**:
```csharp
private static async Task<Results<
    Created<RegisterResponse>,
    ValidationProblem,
    ProblemHttpResult,
    ForbidHttpResult>>
    HandleAsync(
        RegisterRequest request,
        IAuthService authService,
        IOptions<AuthConfiguration> config,
        ILogger<RegisterEndpoint> logger,
        CancellationToken cancellationToken)
{
    // Check if registration is allowed
    if (!config.Value.AllowRegistration)
    {
        logger.LogWarning("Registration attempt rejected (disabled in environment)");
        return TypedResults.Problem(
            title: "Registration is disabled",
            detail: "User registration is not available in this environment.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    // Map to command
    var command = new RegisterUserCommand(
        request.Email,
        request.Password,
        request.DisplayName,
        request.MustChangePassword);

    // Call service
    var result = await authService.RegisterUserAsync(command, cancellationToken);

    // Handle result
    if (!result.Success)
    {
        return result.Error switch
        {
            RegistrationError.EmailAlreadyExists => TypedResults.Problem(
                title: "Email address is already registered",
                detail: "An account with this email address already exists.",
                statusCode: StatusCodes.Status409Conflict),

            RegistrationError.IdentityStoreUnavailable => TypedResults.Problem(
                title: "Service temporarily unavailable",
                detail: "Unable to connect to the identity store. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable),

            _ => TypedResults.Problem(
                title: "Registration failed",
                detail: "An unexpected error occurred during registration.",
                statusCode: StatusCodes.Status400BadRequest)
        };
    }

    // Success - return 201 Created with Location header
    var response = new RegisterResponse(
        result.UserId,
        result.Email!,
        result.DisplayName!,
        result.MustChangePassword);

    logger.LogInformation(
        "User registered successfully: {Email}",
        AnonymizeEmail(result.Email));

    return TypedResults.Created("/api/v1/auth/profile", response);
}

private static string AnonymizeEmail(string? email)
{
    if (string.IsNullOrEmpty(email)) return "[null]";
    var parts = email.Split('@');
    if (parts.Length != 2) return "[invalid]";
    return $"{parts[0][0]}***@{parts[1]}";
}
```

### Step 8: Implement AuthService
**Location**: `src/RSSVibe.Services/Auth/AuthService.cs`

**Important**: Class must be `internal sealed`

**Key methods**:

```csharp
public async Task<RegisterUserResult> RegisterUserAsync(
    RegisterUserCommand command,
    CancellationToken cancellationToken = default)
{
    // Check if user exists
    var existingUser = await _userManager.FindByEmailAsync(command.Email);
    if (existingUser is not null)
    {
        return new RegisterUserResult
        {
            Success = false,
            Error = RegistrationError.EmailAlreadyExists
        };
    }

    // Create new user
    var user = new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = command.Email,
        Email = command.Email,
        // Map other properties
    };

    try
    {
        var result = await _userManager.CreateAsync(user, command.Password);

        if (!result.Succeeded)
        {
            // Handle identity errors
        }

        return new RegisterUserResult
        {
            Success = true,
            UserId = user.Id,
            Email = user.Email,
            DisplayName = command.DisplayName,
            MustChangePassword = command.MustChangePassword
        };
    }
    catch (DbException ex)
    {
        _logger.LogError(ex, "Database error during registration");
        return new RegisterUserResult
        {
            Success = false,
            Error = RegistrationError.IdentityStoreUnavailable
        };
    }
}
```

### Step 9: Wire Up Endpoint in Program.cs
**Location**: `src/RSSVibe.ApiService/Program.cs`

**After** `app.UseExceptionHandler()` and **before** `app.Run()`:
```csharp
// Map auth endpoints
app.MapRegisterEndpoint();
```

### Step 10: Add Configuration to appsettings.json
**Location**: `src/RSSVibe.ApiService/appsettings.json`

```json
{
  "Auth": {
    "AllowRegistration": true
  }
}
```

**Production override** (`appsettings.Production.json`):
```json
{
  "Auth": {
    "AllowRegistration": false,
    "RootUserEmail": "${RSSVIBE_ROOT_USER_EMAIL}",
    "RootUserPassword": "${RSSVIBE_ROOT_USER_PASSWORD}"
  }
}
```

### Step 11: Add Integration Tests
**Location**: `Tests/RSSVibe.ApiService.Tests/Endpoints/Auth/RegisterEndpointTests.cs`

**Test cases**:
- `RegisterEndpoint_WithValidRequest_ShouldReturn201Created`
- `RegisterEndpoint_WithInvalidEmail_ShouldReturn400BadRequest`
- `RegisterEndpoint_WithWeakPassword_ShouldReturn400BadRequest`
- `RegisterEndpoint_WithExistingEmail_ShouldReturn409Conflict`
- `RegisterEndpoint_WhenRegistrationDisabled_ShouldReturn403Forbidden`
- `RegisterEndpoint_WhenDatabaseUnavailable_ShouldReturn503ServiceUnavailable`

**Test setup**:
- Use `WebApplicationFactory<Program>` for in-memory hosting
- Use Testcontainers for PostgreSQL instance
- Seed test data for conflict scenarios
- Mock configuration for environment checks

### Step 12: Add Unit Tests for AuthService
**Location**: `Tests/RSSVibe.Services.Tests/Auth/AuthServiceTests.cs`

**Test cases**:
- `RegisterUserAsync_WithValidCommand_ShouldCreateUser`
- `RegisterUserAsync_WithExistingEmail_ShouldReturnEmailAlreadyExistsError`
- `RegisterUserAsync_WhenUserManagerFails_ShouldReturnInvalidPasswordError`
- `RegisterUserAsync_WhenDatabaseThrows_ShouldReturnIdentityStoreUnavailableError`

**Mocking strategy**:
- Mock `UserManager<ApplicationUser>` (use NSubstitute)
- Mock `ILogger<AuthService>`
- Prefer real ApplicationUser instances (no mocking)

### Step 13: Add Logging
**Throughout implementation**:

```csharp
// Success
_logger.LogInformation(
    "User registered successfully: {Email}",
    AnonymizeEmail(command.Email));

// Conflict
_logger.LogInformation(
    "Registration attempt for existing email: {Email}",
    AnonymizeEmail(command.Email));

// Error
_logger.LogError(ex,
    "Database error during user registration for {Email}",
    AnonymizeEmail(command.Email));

// Disabled
_logger.LogWarning(
    "Registration attempt rejected (disabled in environment)");
```

### Step 14: Update API Documentation
**Location**: Update OpenAPI metadata

**Add** to endpoint definition:
```csharp
.WithOpenApi(operation =>
{
    operation.Summary = "Register a new user account";
    operation.Description = "Creates a new user account using email and password. " +
                           "Disabled in production when root user provisioning is enabled.";
    return operation;
});
```

### Step 15: Add DisplayName and MustChangePassword to ApplicationUser
**Location**: `src/RSSVibe.Data/Entities/ApplicationUser.cs`

**Update**:
```csharp
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public bool MustChangePassword { get; set; }
}
```

**Note**: Requires database migration after this change

### Step 16: Create and Apply Database Migration
**Command**:
```bash
cd src/RSSVibe.Data
bash add_migration.sh AddDisplayNameAndMustChangePasswordToApplicationUser
```

**Review migration** before applying to ensure it adds columns correctly.

### Step 17: Manual Testing
**Test scenarios**:
1. Successful registration with valid data
2. Validation errors (invalid email, weak password, etc.)
3. Duplicate email registration
4. Registration when disabled (modify config)
5. Verify Location header in response
6. Verify password is hashed in database

**Tools**:
- curl, Postman, or HTTP client
- PostgreSQL client to inspect AspNetUsers table

### Step 18: Security Review
**Checklist**:
- [ ] Passwords never logged
- [ ] Generic error messages for enumeration prevention
- [ ] Environment check prevents production registration
- [ ] HTTPS enforced in production (configured elsewhere)
- [ ] Rate limiting considered (future enhancement)
- [ ] Password hashing uses secure algorithm (PBKDF2 via Identity)

### Step 19: Code Review & Refactoring
**Review points**:
- Service layer properly abstracts UserManager
- Command/Result pattern used consistently
- Error handling covers all scenarios
- Logging provides adequate observability
- Code follows project conventions (primary constructors, records, etc.)
- No code smells or technical debt introduced

### Step 20: Documentation Updates
**Update**:
- API documentation with endpoint details
- README with registration instructions
- Configuration guide for production deployment
- Security considerations document

---

## Summary Checklist

### Project Setup
- [ ] RSSVibe.Services project has reference to RSSVibe.Data
- [ ] RSSVibe.ApiService project has reference to RSSVibe.Services
- [ ] Required NuGet packages added to RSSVibe.Services (Identity, DI abstractions)

### Service Layer
- [ ] IAuthService interface created (public) in `src/RSSVibe.Services/Auth/`
- [ ] AuthService implementation completed (internal sealed) in `src/RSSVibe.Services/Auth/`
- [ ] RegisterUserCommand and RegisterUserResult types created (public)
- [ ] RegistrationError enum created (public)
- [ ] ServiceCollectionExtensions created in `src/RSSVibe.Services/Extensions/`
- [ ] AddRssVibeServices() extension method implemented
- [ ] AuthService registered via AddRssVibeServices()

### API Layer
- [ ] RegisterEndpoint minimal API created in `src/RSSVibe.ApiService/Endpoints/Auth/`
- [ ] Endpoint uses TypedResults with explicit return type
- [ ] AuthConfiguration model created in `src/RSSVibe.ApiService/Configuration/`
- [ ] AddRssVibeServices() called in Program.cs
- [ ] Endpoint mapped in Program.cs
- [ ] Identity password options configured

### Data Layer
- [ ] ApplicationUser extended with DisplayName and MustChangePassword

### Database Changes
- [ ] Migration created for ApplicationUser changes
- [ ] Migration reviewed and applied

### Configuration
- [ ] appsettings.json updated with Auth section
- [ ] appsettings.Production.json configured for disabled registration

### Testing
- [ ] Integration tests implemented in `Tests/RSSVibe.ApiService.Tests/` (6 test cases)
- [ ] Unit tests implemented in `Tests/RSSVibe.Services.Tests/` (4 test cases)
- [ ] Test projects have references to tested projects
- [ ] Manual testing completed
- [ ] All tests passing

### Quality Assurance
- [ ] Code follows project conventions (AGENTS.md)
- [ ] Service implementations are `internal sealed`
- [ ] Interfaces and models are `public`
- [ ] Extension method pattern used for service registration
- [ ] TypedResults used for all endpoint returns
- [ ] Logging implemented throughout
- [ ] Security review completed
- [ ] Error handling comprehensive
- [ ] Documentation updated
- [ ] Code reviewed and approved

### Deployment Readiness
- [ ] Build succeeds with no warnings
- [ ] All tests pass
- [ ] Configuration validated for all environments
- [ ] Security checklist completed
