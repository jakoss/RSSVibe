# AI Rules for RSSVibe

RSSVibe is a project that aims to provide a seamless and personalized news aggregation experience for users. The platform leverages advanced AI algorithms to curate and deliver relevant content from various sources, ensuring users stay informed about the topics that matter most to them.

## CODING_PRACTICES

### Guidelines for SUPPORT_LEVEL

#### SUPPORT_EXPERT

- Favor elegant, maintainable solutions over verbose code. Assume understanding of language idioms and design patterns.
- Highlight potential performance implications and optimization opportunities in suggested code.
- Frame solutions within broader architectural contexts and suggest design alternatives when appropriate.
- Focus comments on 'why' not 'what' - assume code readability through well-named functions and variables.
- Proactively address edge cases, race conditions, and security considerations without being prompted.
- When debugging, provide targeted diagnostic approaches rather than shotgun solutions.
- Suggest comprehensive testing strategies rather than just example tests, including considerations for mocking, test organization, and coverage.


### Guidelines for VERSION_CONTROL

#### GIT

- Use conventional commits to create meaningful commit messages
- Use feature branches with descriptive names following feataure/{short-description} or bugfix/{short-description} conventions
- Write meaningful commit messages that explain why changes were made, not just what
- Keep commits focused on single logical changes to facilitate code review and bisection

### Guidelines for ARCHITECTURE

#### ADR

- Create ADRs in /docs/adr/{name}.md for:
- 1) Major dependency changes
- 2) Architectural pattern changes
- 3) New integration patterns
- 4) Database schema changes

## BACKEND

### Guidelines for DOTNET

#### CODING STYLE
- Language: C# 13 on .NET SDK `9.x` (see `global.json`).
- Style: enforced via `.editorconfig` and analyzers; keep code warning-free (TreatWarningsAsErrors=true).
- Indentation: 4 spaces; use expression-bodied members when clear; prefer `readonly`/`sealed`.
- Naming: PascalCase for types/methods, camelCase for locals/parameters, `_camelCase` for private fields; namespaces match folder and project names.
- Modern C#:
  - Primary constructors: `public sealed class Foo(IBar bar) : IFoo`.
  - Collection initializers: `opt.OAuthScopes([.. settings.Scopes.Keys]);`.
  - Pattern matching: `if (obj is IFoo foo) { /* ... */ }`.
  - Records for immutables: `public record Employee(int Id, string Name);`.
  - Null checks: `if (obj is null)` / `if (obj is not null)`.

#### TESTING

- Framework: TUnit
- Location: unit/integration projects under `Tests/`. Test projects mirror main projects with suffix `.Tests`.
- Naming: `ClassName_MethodName_ShouldBehavior` for facts; integration tests may use Testcontainers—Docker required locally.
- Avoid mocking unless necessary; prefer real implementations for integration tests. If mocking is needed, use NSubstitute.
- Use WebApplicationFactory for in-memory hosting during integration tests.
- Use Testcontainers for running databases in tests.

## COMMANDS FOR BUILD AND TEST
- `dotnet restore`: restores all solution packages.
- `dotnet build -c Release -p:TreatWarningsAsErrors=true`: builds code with warnings treated as errors. This should be always used in CI/CD and before pull requests.
- `dotnet test`: runs tests.
- `dotnet format`: applies code style from `.editorconfig` and analyzers. If this command fails without a clear reason - use `dotnet format --verify-no-changes` that will output errors that couldn't be fixed automatically.

#### ENTITY FRAMEWORK

- Use the repository and unit of work patterns to abstract data access logic and simplify testing
- Implement eager loading with Include() to avoid N+1 query problems
- Use migrations for database schema changes and version control with proper naming conventions
- Apply appropriate tracking behavior (AsNoTracking() for read-only queries) to optimize performance
- Implement query optimization techniques like compiled queries for frequently executed database operations
- Configure entities using Fluent API, avoiding data annotations for better separation of concerns
- **Type-Safe JSON Properties**: Never use string properties in entities for JSON data. Always create dedicated strongly-typed model classes in `Models/` directory and use EF Core's `OwnsOne()` or `OwnsMany()` with `ToJson()` for JSONB storage. This provides compile-time type safety, IntelliSense support, and better maintainability.
  - Example: Instead of `string Selectors`, use `FeedSelectors Selectors` with a dedicated model class
  - Configure with: `builder.OwnsOne(x => x.Selectors, b => b.ToJson());`
- **Minimal Configuration Approach**: Only configure what EF Core cannot infer automatically. Avoid explicit configuration for table names, column names, standard types, and required/nullable properties that EF Core handles via conventions. This keeps configurations clean and focused on meaningful business rules.
  - ❌ Don't: `.HasColumnName("id")`, `.HasColumnType("text")`, `.IsRequired()` for non-nullable properties, `.ToTable("feed")`
  - ✅ Do: `.HasMaxLength(200)`, `.HasDefaultValue(60)`, `.HasDefaultValueSql("now()")`, `.HasCheckConstraint(...)`, `.ValueGeneratedNever()` for GUIDs
  - Use `ConfigureConventions()` for global settings like enum-to-string conversion instead of per-property configuration
  - Only specify column types when using database-specific features (e.g., `jsonb`, `text[]` for PostgreSQL arrays)
- **Creating Migrations**: Use the `add_migration.sh` script located in `src/RSSVibe.Data/` to create new migrations. This script must be executed from the `src/RSSVibe.Data/` directory.
  - Usage: `bash add_migration.sh <MigrationName>` (use PascalCase for migration names)
  - Example: `cd src/RSSVibe.Data && bash add_migration.sh AddUserPreferences`
  - The script handles the correct project and startup project paths automatically
  - Review the generated migration file before applying it to ensure correctness
  - Never modify migration files manually after generation - if changes are needed, remove the migration and regenerate it

#### ASP.NET

- Use minimal APIs for endpoints 
- Apply proper response caching with cache profiles and ETags for improved performance on high traffic endpoints
- Implement proper exception handling with ExceptionFilter or middleware to provide consistent error responses
- Use dependency injection with scoped lifetime for request-specific services and singleton for stateless services

## DATABASE

### Guidelines for SQL

#### POSTGRES

- Use connection pooling to manage database connections efficiently
- Implement JSONB columns for semi-structured data instead of creating many tables for data with flexible schemas
- Use indexes on frequently queried columns to improve read performance
