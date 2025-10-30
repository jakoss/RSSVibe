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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Authenticate user credentials";
                operation.Description = "Authenticates user with email and password. " +
                    "Returns JWT access token and refresh token for subsequent API calls. " +
                    "Supports 'remember me' to extend refresh token lifetime to 30 days.";
                return operation;
            })
            .Produces<LoginResponse>(StatusCodes.Status200OK)
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
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        // Map request to command
        var command = new LoginCommand(
            request.Email,
            request.Password,
            request.RememberMe);

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
