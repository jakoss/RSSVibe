# AI Agent Instructions for RSSVibe

**Project Context**: RSSVibe is a news aggregation platform that provides personalized content curation using AI algorithms to help users stay informed about topics that matter to them.

---

## GENERAL CODING PRINCIPLES

**Support Level: EXPERT** - Assume advanced knowledge of language idioms and design patterns.

- MUST favor elegant, maintainable solutions over verbose code
- MUST proactively address edge cases, race conditions, and security considerations
- MUST focus comments on 'why' not 'what' (code should be self-documenting)
- SHOULD highlight performance implications and optimization opportunities
- SHOULD frame solutions within broader architectural contexts
- SHOULD provide targeted diagnostic approaches when debugging (not shotgun solutions)
- SHOULD suggest comprehensive testing strategies including mocking, organization, and coverage

---

## GIT & VERSION CONTROL

**Branch Naming**: `feature/{short-description}` or `bugfix/{short-description}`

- MUST use conventional commits for all commit messages
- MUST write commit messages explaining WHY changes were made, not just what
- MUST keep commits focused on single logical changes for easier review and bisection
- Example: `feat: add user preference caching to improve feed load times`

---

## ARCHITECTURE DECISION RECORDS (ADR)

**Location**: `/docs/adr/{name}.md`

MUST create ADRs for:
1. Major dependency changes
2. Architectural pattern changes
3. New integration patterns
4. Database schema changes

---

## Querying Microsoft Documentation

You have access to MCP tools called `microsoft_docs_search`, `microsoft_docs_fetch`, and `microsoft_code_sample_search` - these tools allow you to search through and fetch Microsoft's latest official documentation and code samples, and that information might be more detailed or newer than what's in your training data set.

When handling questions around how to work with native Microsoft technologies, such as C#, F#, ASP.NET Core, Microsoft.Extensions, NuGet, Entity Framework, the `dotnet` runtime - please use these tools for research purposes when dealing with specific / narrowly defined questions that may occur.

---

## C# / .NET CODING STANDARDS

**Runtime**: C# 13 on .NET SDK 9.x (see `global.json`)

### Code Style
- MUST keep code warning-free (`TreatWarningsAsErrors=true`)
- MUST use 4 spaces for indentation
- MUST prefer `readonly` and `sealed` where applicable
- MUST follow naming conventions:
  - `PascalCase` for types and methods
  - `camelCase` for locals and parameters
  - `_camelCase` for private fields
- Namespaces MUST match folder and project names
- MUST declare API request/response models as positional records (constructor parameters over property setters) for immutability and clarity
- MUST define FluentValidation validator types as nested classes named `Validator` inside the validated type so they can be referenced via `ValidatedType.Validator`

### Modern C# Patterns (REQUIRED)
```csharp
// Primary constructors
public sealed class Foo(IBar bar) : IFoo

// Collection initializers with spread
opt.OAuthScopes([.. settings.Scopes.Keys]);

// Pattern matching
if (obj is IFoo foo) { /* ... */ }

// Records for immutable data
public record Employee(int Id, string Name);

// Null checks
if (obj is null)
if (obj is not null)
```

---

## TESTING STANDARDS

**Framework**: TUnit
**Location**: `Tests/` directory with `.Tests` suffix matching main project

### Test Organization
- MUST name test methods: `ClassName_MethodName_ShouldBehavior`
- MUST prefer real implementations over mocks (use NSubstitute only when necessary)
- MUST use `WebApplicationFactory` for in-memory API hosting in integration tests
- MUST use Testcontainers for database tests (Docker required locally)

### Test Strategy
- SHOULD write integration tests that verify real behavior end-to-end
- SHOULD consider mocking strategy, test organization, and coverage comprehensively
- AVOID unnecessary mocking that obscures real behavior

---

## BUILD & TEST COMMANDS

```bash
# Restore packages
dotnet restore

# Build with warnings as errors (REQUIRED before PRs)
dotnet build -c Release -p:TreatWarningsAsErrors=true

# Run tests
dotnet test

# Apply code style from .editorconfig
dotnet format

# Verify formatting (shows unfixable errors)
dotnet format --verify-no-changes
```

---

## ENTITY FRAMEWORK CORE PATTERNS

### Data Access Patterns
- MUST use repository and unit of work patterns for data access abstraction
- MUST use eager loading with `Include()` to prevent N+1 query problems
- MUST apply `AsNoTracking()` for read-only queries to optimize performance
- MUST configure entities using Fluent API (AVOID data annotations)
- SHOULD implement compiled queries for frequently executed operations

### Type-Safe JSON Properties (CRITICAL)
**NEVER use string properties for JSON data**

❌ **WRONG**:
```csharp
public class Feed {
    public string Selectors { get; set; }  // Don't do this!
}
```

✅ **CORRECT**:
```csharp
// Create strongly-typed model in Models/ directory
public class FeedSelectors {
    public string Title { get; set; }
    public string Content { get; set; }
}

public class Feed {
    public FeedSelectors Selectors { get; set; }  // Type-safe!
}

// Configure with Fluent API
builder.OwnsOne(x => x.Selectors, b => b.ToJson());
```

**Benefits**: Compile-time type safety, IntelliSense support, better maintainability

### Minimal Configuration Approach
**ONLY configure what EF Core cannot infer automatically**

❌ **AVOID** (EF Core infers these automatically):
```csharp
.HasColumnName("id")
.HasColumnType("text")
.IsRequired()  // for non-nullable properties
.ToTable("feed")
```

✅ **DO** (meaningful business rules only):
```csharp
.HasMaxLength(200)
.HasDefaultValue(60)
.HasDefaultValueSql("now()")
.HasCheckConstraint("check_positive", "value > 0")
.ValueGeneratedNever()  // for GUIDs
```

- ONLY specify column types for database-specific features (e.g., `jsonb`, `text[]` for PostgreSQL)

### Migration Management

**MUST use the migration script**: `src/RSSVibe.Data/add_migration.sh`

```bash
# Navigate to Data project directory
cd src/RSSVibe.Data

# Create migration (use PascalCase)
bash add_migration.sh AddUserPreferences
```

- MUST execute script from `src/RSSVibe.Data/` directory
- MUST review generated migration file before applying
- NEVER modify migration files manually (remove and regenerate instead)
- Script handles correct project and startup project paths automatically

---

## PROJECT ARCHITECTURE

### Service Layer (`RSSVibe.Services`)
- MUST implement all business logic in the `RSSVibe.Services` project
- MUST define service interfaces (e.g., `IAuthService`, `IFeedService`) for dependency injection
- MUST use command/result patterns for service operations
- Services SHOULD be organized by domain area in folders (e.g., `Auth/`, `Feeds/`)
- MUST inject repositories, `UserManager`, and other infrastructure dependencies into services
- SHOULD use primary constructors for service classes
- Service implementations MUST be `internal sealed` (only interfaces and models are `public`)
- Each project MUST provide an `IServiceCollection` extension method to register its services

### Project Responsibilities

| Project | Responsibility |
|---------|---------------|
| `RSSVibe.Contracts` | API request/response DTOs, shared domain models |
| `RSSVibe.Services` | Business logic, validation, orchestration |
| `RSSVibe.Data` | Entity models, DbContext, configurations, migrations |
| `RSSVibe.ApiService` | Minimal API endpoints, routing, middleware |
| `RSSVibe.Web` | Blazor UI components and pages |

### Service Layer Patterns
```csharp
// Service interface (PUBLIC)
public interface IAuthService
{
    Task<RegisterUserResult> RegisterUserAsync(RegisterUserCommand command, CancellationToken ct);
}

// Service implementation with primary constructor (INTERNAL SEALED)
internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<RegisterUserResult> RegisterUserAsync(
        RegisterUserCommand command,
        CancellationToken ct)
    {
        // Business logic here
    }
}

// Command model (PUBLIC - in same file as service or separate Commands/ folder)
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    bool MustChangePassword
);

// Result model (PUBLIC - in same file as service or separate Results/ folder)
public sealed record RegisterUserResult
{
    public bool Success { get; init; }
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public RegistrationError? Error { get; init; }
}
```

### Service Registration Pattern
**Each project MUST provide an extension method to register its services**

**Location**: `{ProjectName}/Extensions/ServiceCollectionExtensions.cs`

```csharp
// In RSSVibe.Services/Extensions/ServiceCollectionExtensions.cs
namespace RSSVibe.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRssVibeServices(this IServiceCollection services)
    {
        // Register all services from this project
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFeedService, FeedService>();
        // ... other services

        return services;
    }
}

// In Program.cs (RSSVibe.ApiService)
builder.Services.AddRssVibeServices(); // Single call registers all services
```

**Benefits**:
- Encapsulates service registration logic within each project
- `Program.cs` remains clean with single method calls per project
- Internal implementations hidden from consuming projects
- Easy to maintain and test service registration

---

## ASP.NET CORE WEB API

### API Design
- MUST use minimal APIs for endpoints
- MUST organize minimal API endpoints with one endpoint per file and folder hierarchy mirroring the route structure
- Endpoints SHOULD be thin wrappers that delegate to services in `RSSVibe.Services`
- MUST use `TypedResults` for all endpoint return types (NOT `Results` or `IResult`)
- MUST implement proper exception handling with ExceptionFilter or middleware for consistent error responses
- MUST validate inbound requests with FluentValidation and rely on SharpGrip.FluentValidation.AutoValidation.Endpoints to run validators automatically before handlers execute
- SHOULD apply response caching with cache profiles and ETags for high-traffic endpoints

### Hierarchical Endpoint Organization

**All API endpoints MUST be organized in a hierarchical group structure** for maintainability and shared configuration.

**Structure Overview**:
```
/api/v1 (ApiGroup) ← Root API version group
  └── /auth (AuthGroup) ← Feature group
        ├── /register (RegisterEndpoint)
        ├── /login (LoginEndpoint)
        └── /refresh-token (RefreshTokenEndpoint)
  └── /feeds (FeedsGroup) ← Feature group
        ├── /list (ListFeedsEndpoint)
        ├── /{id} (GetFeedEndpoint)
        └── ... more endpoints
  └── /users (UsersGroup) ← Feature group
        └── ... endpoints
```

**Implementation Pattern**:

**1. Root API Group (Program.cs)**:
```csharp
// In Program.cs, register root API group
app.MapApiV1();

// ApiGroup.cs
namespace RSSVibe.ApiService.Endpoints;

public static class ApiGroup
{
    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        // Register all feature groups
        group.MapAuthGroup();
        group.MapFeedsGroup();
        group.MapUsersGroup();

        return endpoints;
    }
}
```

**2. Feature Group (e.g., AuthGroup.cs)**:
```csharp
// In Endpoints/Auth/AuthGroup.cs
namespace RSSVibe.ApiService.Endpoints.Auth;

public static class AuthGroup
{
    public static IEndpointRouteBuilder MapAuthGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth")
            .WithTags("Auth");

        // Register all endpoints in this group
        group.MapRegisterEndpoint();
        group.MapLoginEndpoint();
        group.MapRefreshTokenEndpoint();

        return endpoints;
    }
}
```

**3. Individual Endpoint (e.g., RegisterEndpoint.cs)**:
```csharp
// In Endpoints/Auth/RegisterEndpoint.cs
namespace RSSVibe.ApiService.Endpoints.Auth;

public static class RegisterEndpoint
{
    /// Parameter type is RouteGroupBuilder for composability
    public static RouteGroupBuilder MapRegisterEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("Register")
            .WithOpenApi(...);

        return group;
    }

    private static async Task<Results<...>> HandleAsync(...)
    {
        // Handler implementation
    }
}
```

**Benefits of Hierarchical Structure**:
- **Tree-like organization**: Mirrors URL structure in code
- **Shared configuration**: Apply validation, authentication, CORS at group level
- **Scalability**: Easy to add new feature groups and endpoints
- **Maintainability**: Clear ownership of endpoint groups by feature area
- **OpenAPI documentation**: Groups automatically organize endpoints in Swagger/OpenAPI

**Rules**:
- MUST create one Group class per feature folder (e.g., `AuthGroup`, `FeedsGroup`)
- Group classes MUST extend `IEndpointRouteBuilder` and take a parameter of same type
- Individual endpoint methods MUST accept `RouteGroupBuilder` parameter (not `IEndpointRouteBuilder`)
- Individual endpoint methods MUST return `RouteGroupBuilder` for method chaining
- Groups MUST be registered in parent group, starting from root `ApiGroup`

### TypedResults Pattern
**ALWAYS use TypedResults for type-safe responses and better OpenAPI documentation**

```csharp
// Endpoint signature with explicit return type
private static async Task<Results<Ok<RegisterResponse>, Conflict, ServiceUnavailable, ForbidHttpResult>>
    HandleAsync(...)
{
    // Use TypedResults for all returns
    if (!config.Value.AllowRegistration)
    {
        return TypedResults.Forbid(); // NOT Results.Forbid()
    }

    if (result.Error == RegistrationError.EmailAlreadyExists)
    {
        return TypedResults.Conflict(); // NOT Results.Conflict()
    }

    var response = new RegisterResponse(...);
    return TypedResults.Ok(response); // NOT Results.Ok(response)
}
```

**Benefits**:
- Compile-time type safety for response types
- Automatic OpenAPI documentation generation
- IntelliSense support for response types
- Union types (`Results<T1, T2, T3>`) document all possible responses

### Dependency Injection
- MUST use scoped lifetime for request-specific services
- MUST use singleton lifetime for stateless services
- MUST register services via extension methods (e.g., `AddRssVibeServices()`)
- Extension methods SHOULD be named `Add{ProjectName}` (e.g., `AddRssVibeServices`, `AddRssVibeDatabase`)
- Service implementations MUST be `internal sealed` to prevent external instantiation

### API Contracts
- MUST define all API request/response models in the `RSSVibe.Contracts` project
- API contracts are shared between frontend and backend services via project reference
- MUST use positional records for all contract models (immutability and clarity)
- MUST document contract changes in commit messages and ADRs when adding new endpoints or modifying existing ones
- Contracts include DTOs for API requests, responses, and domain models exposed to clients

---

## POSTGRESQL DATABASE

### Performance & Design
- MUST use connection pooling for efficient connection management
- MUST use JSONB columns for semi-structured data (avoid creating many tables for flexible schemas)
- MUST create indexes on frequently queried columns to improve read performance

### Query Optimization
- SHOULD monitor query plans for expensive operations
- SHOULD use partial indexes when filtering on specific conditions
- SHOULD consider materialized views for complex aggregations

---

## QUICK REFERENCE

| Task | Command/Pattern |
|------|----------------|
| Build for PR | `dotnet build -c Release -p:TreatWarningsAsErrors=true` |
| Create migration | `cd src/RSSVibe.Data && bash add_migration.sh MigrationName` |
| Test naming | `ClassName_MethodName_ShouldBehavior` |
| Branch naming | `feature/{description}` or `bugfix/{description}` |
| JSON in EF | Create model class + `OwnsOne(x => x.Prop, b => b.ToJson())` |
| Commit style | Conventional commits explaining WHY |
| Service layer | All business logic in `RSSVibe.Services` project |
| Service pattern | Interface + implementation with command/result types |
| Service visibility | Implementations `internal sealed`, interfaces `public` |
| Service registration | Extension methods `Add{ProjectName}` per project |
| Endpoint returns | MUST use `TypedResults` (NOT `Results` or `IResult`) |
| Endpoint groups | Root: `ApiGroup.MapApiV1()`, Features: `AuthGroup.MapAuthGroup()` |
| Group structure | `/api/v1` → `/auth` → `/register` (hierarchical MapGroup calls) |
