namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Registers all auth-related endpoints under the /auth route group.
/// </summary>
public static class AuthGroup
{
    /// <summary>
    /// Maps all auth endpoints to the provided route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthGroup(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth")
            .WithTags("Auth");

        // Register all endpoints in the auth group
        group.MapRegisterEndpoint();
        group.MapLoginEndpoint();

        // Future auth endpoints can be added here
        // group.MapRefreshTokenEndpoint();

        return endpoints;
    }
}
