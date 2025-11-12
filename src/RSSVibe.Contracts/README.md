# RSSVibe API Client

Type-safe HTTP client for the RSSVibe API with automatic bearer token authentication.

## Usage

### Registration

Add the client to your service collection:

```csharp
// With base address
services.AddRSSVibeApiClient("https://api.rssvibe.local");

// Or with configuration
services.AddRSSVibeApiClient(client =>
{
    client.BaseAddress = new Uri("https://api.rssvibe.local");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// With resilience (requires Microsoft.Extensions.Http.Resilience)
services.AddRSSVibeApiClient("https://api.rssvibe.local")
    .AddStandardResilienceHandler();
```

### Authentication

The client automatically injects bearer tokens if `IAccessTokenProvider` is registered:

#### For Blazor WebAssembly

```csharp
using Microsoft.AspNetCore.Components.Authorization;
using RSSVibe.Contracts;

public sealed class ClientAccessTokenProvider(AuthenticationStateProvider authStateProvider) 
    : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user?.Identity?.IsAuthenticated != true)
            return null;

        // Get token from claims (adjust claim type based on your auth implementation)
        return user.FindFirst("access_token")?.Value;
    }
}

// In Program.cs
builder.Services.AddScoped<IAccessTokenProvider, ClientAccessTokenProvider>();
builder.Services.AddRSSVibeApiClient("https+http://apiservice");
```

#### For Blazor Server

```csharp
using Microsoft.AspNetCore.Components.Authorization;
using RSSVibe.Contracts;

public sealed class ServerAccessTokenProvider(
    AuthenticationStateProvider authStateProvider,
    IHttpContextAccessor? httpContextAccessor = null) 
    : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync()
    {
        // Try HTTP context first (for server-side calls)
        if (httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var token = httpContextAccessor.HttpContext.User.FindFirst("access_token")?.Value;
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        // Fallback to AuthenticationStateProvider
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst("access_token")?.Value;
    }
}

// In Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccessTokenProvider, ServerAccessTokenProvider>();
builder.Services.AddRSSVibeApiClient("https+http://apiservice");
```

#### For Blazor Auto (Server + WASM)

Register the client and token provider in **both** projects:

**RSSVibe.Web (Server):**
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccessTokenProvider, ServerAccessTokenProvider>();
builder.Services.AddRSSVibeApiClient("https+http://apiservice");
```

**RSSVibe.Web.Client (WASM):**
```csharp
builder.Services.AddScoped<IAccessTokenProvider, ClientAccessTokenProvider>();
builder.Services.AddRSSVibeApiClient("https+http://apiservice");
```

### Authentication

```csharp
public class AuthExampleService(IRSSVibeApiClient apiClient)
{
    public async Task<string?> LoginAsync()
    {
        var loginRequest = new LoginRequest(
            Email: "user@example.com",
            Password: "password123",
            RememberMe: false
        );

        var result = await apiClient.Auth.LoginAsync(loginRequest);

        if (result.IsSuccess)
        {
            return result.Data!.AccessToken;
        }

        Console.WriteLine($"Login failed: {result.ErrorTitle} - {result.ErrorDetail}");
        return null;
    }

    public async Task<ProfileResponse?> GetProfileAsync()
    {
        // Automatically includes Authorization header from IAccessTokenProvider
        var result = await apiClient.Auth.GetProfileAsync();
        return result.IsSuccess ? result.Data : null;
    }
}
```

### Feed Analyses

```csharp
public class FeedAnalysisExampleService(IRSSVibeApiClient apiClient)
{
    public async Task<Guid?> CreateAnalysisAsync()
    {
        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/feed",
            AiModel: "gpt-4",
            ForceReanalysis: false
        );

        // Automatically includes Authorization header from IAccessTokenProvider
        var result = await apiClient.FeedAnalyses.CreateAsync(request);

        if (result.IsSuccess)
        {
            return result.Data!.AnalysisId;
        }

        return null;
    }

    public async Task<FeedAnalysisDetailResponse?> GetAnalysisAsync(Guid analysisId)
    {
        var result = await apiClient.FeedAnalyses.GetAsync(analysisId);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<ListFeedAnalysesResponse?> ListAnalysesAsync()
    {
        var request = new ListFeedAnalysesRequest(
            Status: FeedAnalysisStatus.Completed,
            Sort: "createdAt:desc",
            Skip: 0,
            Take: 20,
            Search: null
        );

        var result = await apiClient.FeedAnalyses.ListAsync(request);
        return result.IsSuccess ? result.Data : null;
    }
}
```

## API Result

All client methods return an `ApiResult<T>` or `ApiResultNoData`:

```csharp
public sealed record ApiResult<TData>
{
    public bool IsSuccess { get; init; }
    public TData? Data { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorTitle { get; init; }
    public string? ErrorDetail { get; init; }
}
```

## Features

- ✅ Type-safe client with strongly-typed request/response models
- ✅ Clean public interface with internal implementation
- ✅ Automatic JSON serialization/deserialization
- ✅ Automatic bearer token authentication via `IAccessTokenProvider`
- ✅ Problem Details (RFC 7807) error handling
- ✅ Culture-invariant query parameter encoding
- ✅ Supports dependency injection
- ✅ Compatible with `Microsoft.Extensions.Http.Resilience`
- ✅ Works with Blazor Server, WASM, and Auto render modes
