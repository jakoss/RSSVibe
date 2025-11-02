# Testing Standards

**Framework**: TUnit
**Location**: `Tests/` directory with `.Tests` suffix matching main project

---

## Test Organization

- MUST name test methods: `ClassName_MethodName_ShouldBehavior`
- MUST prefer real implementations over mocks (use NSubstitute only when necessary)
- MUST use `WebApplicationFactory` for in-memory API hosting in integration tests
- MUST use Testcontainers for database tests (Docker required locally)

---

## Test Strategy

- SHOULD write integration tests that verify real behavior end-to-end
- SHOULD consider mocking strategy, test organization, and coverage comprehensively
- AVOID unnecessary mocking that obscures real behavior

---

## TUnit Assertion Syntax

TUnit uses a fluent assertion style with `await Assert.That()`:

```csharp
// Equality
await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
await Assert.That(value).IsNotEqualTo(0);

// Null checks
await Assert.That(data).IsNotNull();
await Assert.That(data).IsNull();

// After asserting IsNotNull(), the compiler knows the object is not null
// No need for null-forgiving operator (!) in subsequent code
var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
await Assert.That(responseData).IsNotNull();
await Assert.That(responseData.Email).IsEqualTo(request.Email); // No ! needed

// Boolean checks
await Assert.That(result).IsTrue();
await Assert.That(result).IsFalse();

// Collections
await Assert.That(list).HasCount(5);
await Assert.That(list).IsEmpty();
await Assert.That(list).Contains(item);

// Strings
await Assert.That(text).StartsWith("Hello");
await Assert.That(text).EndsWith("World");
await Assert.That(text).Contains("test");
```

---

## HTTP Client Helpers

```csharp
// GET requests
var response = await client.GetAsync("/api/v1/feeds");
var data = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();

// POST requests with JSON body
var request = new CreateFeedRequest(...);
var response = await client.PostAsJsonAsync("/api/v1/feeds", request);

// PUT requests
await client.PutAsJsonAsync("/api/v1/feeds/{id}", updateRequest);

// DELETE requests
await client.DeleteAsync("/api/v1/feeds/{id}");

// Check response status
await Assert.That(response.IsSuccessStatusCode).IsTrue();
await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

// Check response headers
await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("/api/v1/auth/profile");
```

---

## Best Practices

**DO**:
- ✅ Test one scenario per test method
- ✅ Use descriptive test names that explain the scenario
- ✅ Use unique identifiers (`Guid.CreateVersion7()`) to avoid test conflicts
- ✅ Test actual HTTP responses, not internal implementation details
- ✅ Verify response status codes, headers, and body content
- ✅ Test validation rules comprehensively
- ✅ Test error cases as thoroughly as success cases
- ✅ Use `await Assert.That()` for all assertions

**DON'T**:
- ❌ Mock services in endpoint integration tests (use real implementations)
- ❌ Test multiple unrelated scenarios in one test method
- ❌ Hard-code test data that causes conflicts between tests
- ❌ Rely on test execution order (tests should be independent)
- ❌ Skip validation and error case testing
- ❌ Test internal implementation details (test the public API contract)
