# ASP.NET Core Web API Design

---

## API Design Principles

- MUST use minimal APIs for endpoints
- MUST organize minimal API endpoints with one endpoint per file and folder hierarchy mirroring the route structure
- Endpoints SHOULD be thin wrappers that delegate to services in `RSSVibe.Services`
- MUST use `TypedResults` for all endpoint return types (NOT `Results` or `IResult`)
- MUST implement proper exception handling with ExceptionFilter or middleware for consistent error responses
- MUST validate inbound requests with FluentValidation and rely on SharpGrip.FluentValidation.AutoValidation.Endpoints to run validators automatically before handlers execute
- SHOULD apply response caching with cache profiles and ETags for high-traffic endpoints

---

## Hierarchical Endpoint Organization

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

---

## TypedResults Pattern

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
