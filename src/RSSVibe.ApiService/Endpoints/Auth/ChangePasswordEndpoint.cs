using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for changing user passwords.
/// </summary>
public static class ChangePasswordEndpoint
{
    /// <summary>
    /// Maps the change password endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapChangePasswordEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/change-password", HandleAsync)
            .WithName("ChangePassword")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Change user password";
                operation.Description = "Changes the authenticated user's password after verifying the current password. Revokes all existing refresh tokens for security.";
                return operation;
            })
            .RequireAuthorization()
            .RequireRateLimiting("password-change")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    /// <summary>
    /// Handles the change password request.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        // Extract userId from JWT claims
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            // This should never happen with proper JWT validation, but handle gracefully
            return TypedResults.Problem(
                title: "Authentication failed",
                detail: "Invalid user identity in token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Create command and call service
        var command = new ChangePasswordCommand(
            UserId: userId,
            CurrentPassword: request.CurrentPassword,
            NewPassword: request.NewPassword);

        var result = await authService.ChangePasswordAsync(command, cancellationToken);

        // Handle service result
        return result.Success
            ? TypedResults.NoContent()
            : result.Error switch
            {
                ChangePasswordError.InvalidCurrentPassword => TypedResults.Problem(
                    title: "Authentication failed",
                    detail: "Current password is incorrect.",
                    statusCode: StatusCodes.Status401Unauthorized),

                ChangePasswordError.WeakPassword => TypedResults.Problem(
                    title: "Invalid password",
                    detail: "New password does not meet complexity requirements.",
                    statusCode: StatusCodes.Status400BadRequest),

                ChangePasswordError.IdentityStoreUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to connect to the identity store. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "An unexpected error occurred. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable)
            };
    }
}
