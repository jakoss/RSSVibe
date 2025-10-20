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

## ASP.NET CORE WEB API

### API Design
- MUST use minimal APIs for endpoints
- MUST organize minimal API endpoints with one endpoint per file and folder hierarchy mirroring the route structure
- MUST implement proper exception handling with ExceptionFilter or middleware for consistent error responses
- MUST validate inbound requests with FluentValidation and rely on SharpGrip.FluentValidation.AutoValidation.Endpoints to run validators automatically before handlers execute
- SHOULD apply response caching with cache profiles and ETags for high-traffic endpoints

### Dependency Injection
- MUST use scoped lifetime for request-specific services
- MUST use singleton lifetime for stateless services

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
