using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for refreshing JWT access tokens using refresh tokens.
/// </summary>
public static class RefreshEndpoint
{
    /// <summary>
    /// Maps the refresh token endpoint to the route group.
    /// </summary>
     public static RouteGroupBuilder MapRefreshEndpoint(this RouteGroupBuilder group)
     {
         group.MapPost("/refresh", HandleAsync)
             .WithName("RefreshToken")
             .WithSummary("Refresh JWT access token")
             .WithDescription("""
                 Exchanges a valid refresh token for a new access token and refresh token pair. 
                 Implements token rotation for security - the old refresh token is invalidated. 
                 Detects and prevents replay attacks by revoking all user tokens if a used token is presented.
                 """)
             .Produces<RefreshTokenResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

         return group;
     }

    private static async Task<Results<
        Ok<RefreshTokenResponse>,
        ProblemHttpResult>>
        HandleAsync(
            RefreshTokenRequest request,
            IAuthService authService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        // Map request to command
        var command = new RefreshTokenCommand(request.RefreshToken);

        var logger = loggerFactory.CreateLogger("RefreshEndpoint");

        // Call service
        var result = await authService.RefreshTokenAsync(command, cancellationToken);

        // Handle result
        if (!result.Success)
        {
            return result.Error switch
            {
                RefreshTokenError.TokenInvalid => TypedResults.Problem(
                    title: "Invalid token",
                    detail: "The refresh token is invalid, expired, or has been revoked.",
                    statusCode: StatusCodes.Status401Unauthorized),

                RefreshTokenError.TokenReplayDetected => TypedResults.Problem(
                    title: "Token replay detected",
                    detail: "The refresh token has already been used. All tokens for this user have been revoked for security reasons. Please log in again.",
                    statusCode: StatusCodes.Status409Conflict),

                RefreshTokenError.ServiceUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to process token refresh. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Token refresh failed",
                    detail: "An unexpected error occurred during token refresh.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // Success - return 200 OK with new tokens
        var response = new RefreshTokenResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresInSeconds,
            result.MustChangePassword);

        logger.LogInformation("Token refreshed successfully");

        return TypedResults.Ok(response);
    }
}
