# Endpoint Integration Testing

---

## Test Infrastructure Overview

RSSVibe uses a **real integration testing approach** with actual database and services, not mocks. This provides high confidence that the system works correctly end-to-end.

**Key Components**:
1. **PostgresTestContainer** - Manages PostgreSQL Docker container lifecycle
2. **TestApplication** - Custom `WebApplicationFactory<Program>` that configures the app with test database
3. **TestsBase** - Base class providing shared test infrastructure via `ClassDataSource`

**Shared Resource Pattern**:
- Test containers and application factory are shared across all tests in a session using `SharedType.PerTestSession`
- Reduces test execution time by avoiding repeated container startup/teardown
- Database migrations run once during `TestApplication.InitializeAsync()`

---

## Authenticated Test User

**Test Infrastructure provides a pre-configured authenticated user** for testing protected endpoints.

**Test User Details** (created during `TestApplication.InitializeAsync()`):
- **Email**: `TestApplication.TestUserEmail` = `"test@rssvibe.local"`
- **Password**: `TestApplication.TestUserPassword` = `"TestPassword123!"`
- **Display Name**: `TestApplication.TestUserDisplayName` = `"Test User"`
- **JWT Token**: `TestApplication.TestUserBearerToken` (automatically generated)

**Creating Authenticated Requests**:

```csharp
// Use CreateAuthenticatedClient() for protected endpoints
var client = CreateAuthenticatedClient();
var response = await client.GetAsync("/api/v1/protected-endpoint");

// Use CreateClient() for public/anonymous endpoints
var client = WebApplicationFactory.CreateClient();
var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
```

**Testing Authentication Scenarios**:

```csharp
[Test]
public async Task ProtectedEndpoint_WithValidToken_ShouldReturnSuccess()
{
    // Arrange - Use authenticated client
    var client = CreateAuthenticatedClient();

    // Act
    var response = await client.GetAsync("/api/v1/user/profile");

    // Assert
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
}

[Test]
public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
{
    // Arrange - Use unauthenticated client
    var client = WebApplicationFactory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/v1/user/profile");

    // Assert
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
}
```

**Important Notes**:
- Test user is created once per test session (shared across all tests)
- JWT token is valid for 60 minutes in test environment
- DO NOT modify the test user in tests (read-only usage)
- For testing user-specific scenarios, create additional test users with unique credentials

---

## Test File Organization

**Pattern**: Mirror the endpoint folder structure under `Tests/` directory

```
Tests/RSSVibe.ApiService.Tests/
  └── Endpoints/
      └── Auth/
          ├── RegisterEndpointTests.cs
          ├── LoginEndpointTests.cs
          └── RefreshTokenEndpointTests.cs
      └── Feeds/
          ├── ListFeedsEndpointTests.cs
          └── CreateFeedEndpointTests.cs
```

**Naming Convention**: `{EndpointName}Tests.cs`

---

## Test Class Structure

```csharp
using System.Net;
using System.Net.Http.Json;
using RSSVibe.Contracts.Auth;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the RegisterEndpoint (/api/v1/auth/register).
/// Tests use real WebApplicationFactory with database and services.
/// </summary>
public class RegisterEndpointTests : TestsBase
{
    [Test]
    public async Task RegisterEndpoint_WithValidRequest_ShouldReturnCreatedWithUserData()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Email).IsEqualTo(request.Email);
        await Assert.That(responseData.DisplayName).IsEqualTo(request.DisplayName);
        await Assert.That(responseData.UserId).IsNotEqualTo(Guid.Empty);
    }

    // Additional test methods...
}
```

---

## Test Method Naming Convention

**Format**: `{EndpointName}_{Scenario}_{ExpectedBehavior}`

**Examples**:
- `RegisterEndpoint_WithValidRequest_ShouldReturnCreatedWithUserData`
- `RegisterEndpoint_WithDuplicateEmail_ShouldReturnConflict`
- `RegisterEndpoint_WithInvalidEmail_ShouldReturnValidationError`
- `ListFeedsEndpoint_WithPagination_ShouldReturnPagedResults`

**Benefits**:
- Clear test intent from method name alone
- Easy to identify what scenario is being tested
- Groups related tests by endpoint when viewed alphabetically

---

## Test Scenarios to Cover

For each endpoint, SHOULD test the following scenarios (where applicable):

**1. Success Cases**:
- Happy path with valid input
- Edge cases (empty lists, boundary values, optional fields)
- Different valid input combinations

**2. Validation Failures** (400 Bad Request):
- Missing required fields
- Invalid formats (email, dates, etc.)
- Out-of-range values
- Business rule violations

**3. Authentication/Authorization** (401/403):
- Unauthenticated requests (use `WebApplicationFactory.CreateClient()`)
- Authenticated requests (use `CreateAuthenticatedClient()`)
- Insufficient permissions
- Expired/invalid tokens

**4. Resource States** (404/409):
- Resource not found
- Resource already exists
- Resource conflicts

**5. Service Errors** (500/503):
- Database unavailable (can be tested with test infrastructure)
- External service failures

---

## Database State Management

**Important**: Tests share a database instance, so you SHOULD:
- Use unique identifiers (GUIDs, unique emails) to avoid conflicts between tests
- Design tests to be idempotent where possible
- Clean up test data if necessary (though TUnit's parallel execution model minimizes conflicts)

**Example**:
```csharp
// Good: Unique email per test using version 7 GUIDs (time-ordered)
var request = new RegisterRequest(
    Email: $"test_{Guid.CreateVersion7():N}@example.com",
    Password: "SecurePass123!",
    DisplayName: "Test User",
    MustChangePassword: false
);

// Bad: Hard-coded email causes conflicts
var request = new RegisterRequest(
    Email: "test@example.com", // Will conflict with other tests!
    // ...
);
```

**Important**: Always use `Guid.CreateVersion7()` instead of `Guid.NewGuid()` for better database performance (time-ordered GUIDs improve indexing and clustering).

---

## Configuration Testing

Tests inherit application configuration from `appsettings.json`. To test configuration-dependent scenarios, use `WithWebHostBuilder` to create a customized factory with overridden configuration.

**Pattern - Override configuration with WithWebHostBuilder**:
```csharp
[Test]
public async Task RegisterEndpoint_WhenRegistrationDisabled_ShouldReturnForbidden()
{
    // Arrange - Create factory with custom configuration
    var customFactory = WebApplicationFactory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:AllowRegistration", "false" } // Override config
            });
        });
    });

    var client = customFactory.CreateClient();
    var request = new RegisterRequest(
        Email: "test@example.com",
        Password: "SecurePass123!",
        DisplayName: "Test User",
        MustChangePassword: false
    );

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

    // Assert
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
}
```

**Important Notes**:
- `WithWebHostBuilder` creates a new factory instance with the specified customizations
- Configuration overrides are merged with existing configuration (not replaced)
- Each test can create its own customized factory for specific scenarios
- The base `WebApplicationFactory` from `TestsBase` remains unmodified
