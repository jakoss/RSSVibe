using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for user authentication (login).
/// </summary>
public static class LoginEndpoint
{
    /// <summary>
    /// Maps the login endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapLoginEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Authenticate user credentials")
            .WithDescription("""
                 Authenticates user with email and password. 
                 Returns JWT access token and refresh token for subsequent API calls. 
                 Supports 'remember me' to extend refresh token lifetime to 30 days.
                 """)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<
        Ok<LoginResponse>,
        ProblemHttpResult>>
        HandleAsync(
            LoginRequest request,
            IAuthService authService,
            HttpContext httpContext,
            IHostEnvironment environment,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        // Map request to command
        var command = new LoginCommand(
            request.Email,
            request.Password,
            request.RememberMe,
            request.UseCookieAuth);

        var logger = loggerFactory.CreateLogger("LoginEndpoint");

        // Call service
        var result = await authService.LoginAsync(command, cancellationToken);

        // Handle result
        if (!result.Success)
        {
            return result.Error switch
            {
                LoginError.InvalidCredentials => TypedResults.Problem(
                    title: "Authentication failed",
                    detail: "Invalid email or password.",
                    statusCode: StatusCodes.Status401Unauthorized),

                LoginError.AccountLocked => TypedResults.Problem(
                    title: "Account locked",
                    detail: "Account has been locked due to multiple failed login attempts. Please try again later or contact support.",
                    statusCode: StatusCodes.Status423Locked),

                LoginError.IdentityStoreUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to connect to the identity store. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Login failed",
                    detail: "An unexpected error occurred during login.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // If using cookie-based auth, set HttpOnly cookie with the access token
        if (request.UseCookieAuth)
        {
            // Secure flag should match the actual request scheme:
            // - HTTPS requests: Secure=true (required for SameSite=None)
            // - HTTP requests: Secure=false (allows localhost development)
            var isSecure = httpContext.Request.IsHttps;

            // Extract domain from request (e.g., "localhost" from "localhost:5000")
            // This allows cookies to work across different ports of the same domain
            var hostWithoutPort = httpContext.Request.Host.Host;

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                // Use SameSite=None to allow cookies to be sent from WASM frontend to backend API
                // (different origins). This is safe with HttpOnly + Secure flags + CORS credentials.
                SameSite = SameSiteMode.None,
                // Set domain explicitly so cookies work across ports (localhost:3000 → localhost:5000)
                Domain = hostWithoutPort,
                Expires = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds)
            };

            httpContext.Response.Cookies.Append("access_token", result.AccessToken!, cookieOptions);

            // Store refresh token in a separate cookie
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.None,
                Domain = hostWithoutPort,
                Expires = DateTimeOffset.UtcNow.AddDays(request.RememberMe ? 30 : 7)
            };

            httpContext.Response.Cookies.Append("refresh_token", result.RefreshToken!, refreshCookieOptions);
        }

        // Success - return 200 OK with tokens
        var response = new LoginResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresInSeconds,
            result.MustChangePassword);

        logger.LogInformation(
            "User logged in successfully: {Email}",
            AnonymizeEmail(request.Email));

        return TypedResults.Ok(response);
    }

    private static string AnonymizeEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "[null]";
        }

        var parts = email.Split('@');
        if (parts.Length != 2)
        {
            return "[invalid]";
        }

        return $"{parts[0][0]}***@{parts[1]}";
    }
}
