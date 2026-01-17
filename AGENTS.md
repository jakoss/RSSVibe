# AI Agent Instructions for RSSVibe

**Project Context**: RSSVibe is a news aggregation platform that provides personalized content curation using AI algorithms to help users stay informed about topics that matter to them.

---

## DOCUMENTATION STRUCTURE

This is the main reference guide. For specialized topics, **use the skill system** to load detailed, task-specific guidance on-demand.

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

**Runtime**: C# 14 on .NET SDK 10.x (see `global.json`)

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

## BUILD & TEST COMMANDS

```bash
# Restore packages
dotnet restore

# Build with warnings as errors (REQUIRED before PRs)
dotnet build -c Release -p:TreatWarningsAsErrors=true

# Run tests (use dotnet run on test project, not dotnet test)
cd Tests/RSSVibe.ApiService.Tests
dotnet run

# Run tests with filter (e.g., specific test class)
dotnet run --treenode-filter /*/*/LoginEndpointTests/*

# Run all tests from solution root (alternative)
dotnet test

# Apply code style from .editorconfig
dotnet format

# Verify formatting (shows unfixable errors)
dotnet format --verify-no-changes
```

---

## BLAZOR CLIENT & API INTEGRATION

**CRITICAL**: The RSSVibe.Client project MUST use statically-typed API clients, NOT raw HttpClient calls.

### API Client Usage Pattern

**ALWAYS use `IRSSVibeApiClient` injected service** for all API calls in Blazor components:

```csharp
@code {
    [Inject]
    private IRSSVibeApiClient ApiClient { get; set; } = default!;

    private async Task LoadDataAsync()
    {
        // ✅ CORRECT: Use typed client
        var result = await ApiClient.Feeds.ListAsync(
            new ListFeedsRequest(
                Skip: 0,
                Take: 50,
                Sort: "lastParsedAt:desc",
                Status: null,
                Search: null
            ),
            cancellationToken);

        if (result.IsSuccess)
        {
            var feeds = result.Data;
            // Use feeds...
        }

        // ❌ WRONG: Direct HttpClient usage
        var response = await HttpClient.GetAsync("/api/v1/feeds");
    }
}
```

### Available API Clients

- `ApiClient.Auth` - Authentication endpoints (`/api/v1/auth`)
- `ApiClient.FeedAnalyses` - Feed analysis endpoints (`/api/v1/feed-analyses`)
- `ApiClient.Feeds` - Feed and feed items endpoints (`/api/v1/feeds`)

### API Result Pattern

All API methods return `ApiResult<TData>` or `ApiResultNoData`:

```csharp
var result = await ApiClient.Feeds.GetAsync(feedId, ct);

if (result.IsSuccess)
{
    var feed = result.Data; // TData
}
else
{
    // Handle error
    var errorTitle = result.ErrorTitle;
    var errorDetail = result.ErrorDetail;
    var statusCode = result.StatusCode;
}
```

**For complete API client documentation**, see [`src/RSSVibe.Contracts/README.md`](src/RSSVibe.Contracts/README.md).

---

## QUICK REFERENCE

| Task | Command/Pattern |
|------|----------------|
| Build for PR | `dotnet build -c Release -p:TreatWarningsAsErrors=true` |
| Run tests | `cd Tests/{Project}.Tests && dotnet run` |
| Filter tests | `dotnet run --treenode-filter /*/*/TestClass/*` |
| Create migration | `cd src/RSSVibe.Data && bash add_migration.sh MigrationName` |
| Branch naming | `feature/{description}` or `bugfix/{description}` |
| Commit style | Conventional commits explaining WHY |
| Test naming | `EndpointName_Scenario_ExpectedBehavior` |
| Test organization | Mirror endpoint structure under `Tests/Endpoints/` |
| Test assertions | `await Assert.That(value).IsEqualTo(expected)` |
| Authenticated tests | Use `CreateAuthenticatedClient()` for protected endpoints |
| Test user email | `TestApplication.TestUserEmail` = `"test@rssvibe.local"` |
| Service assertions | `await using var scope = WebApplicationFactory.Services.CreateAsyncScope();`<br/>`var service = scope.ServiceProvider.GetRequiredService<Service>();` |
| GUID generation | MUST use `Guid.CreateVersion7()` (NOT `Guid.NewGuid()`) |
| JSON in EF | Create model class + `OwnsOne(x => x.Prop, b => b.ToJson())` |
| **Blazor API calls** | **MUST use `ApiClient.Feeds.ListAsync()` (NOT `HttpClient.GetAsync()`)** |
| API client access | `ApiClient.Auth`, `ApiClient.Feeds`, `ApiClient.FeedAnalyses` |
| Service layer | All business logic in `RSSVibe.Services` project |
| Service pattern | Interface + implementation with command/result types |
| Service visibility | Implementations `internal sealed`, interfaces `public` |
| Service registration | Extension methods `Add{ProjectName}` per project |
| Endpoint returns | MUST use `TypedResults` (NOT `Results` or `IResult`) |
| Endpoint groups | Root: `ApiGroup.MapApiV1()`, Features: `AuthGroup.MapAuthGroup()` |
| Group structure | `/api/v1` → `/auth` → `/register` (hierarchical MapGroup calls) |

---

## SPECIALIZED TOPICS

For detailed guidance on specific areas, **use the skill system** to load comprehensive, task-specific documentation on-demand.
