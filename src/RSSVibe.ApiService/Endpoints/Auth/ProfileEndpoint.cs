using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;
using System.Security.Claims;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for retrieving authenticated user's profile.
/// </summary>
public static class ProfileEndpoint
{
    /// <summary>
    /// Maps the profile endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapProfileEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", HandleAsync)
            .WithName("GetUserProfile")
            .RequireAuthorization()
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get current user profile";
                operation.Description = "Retrieves the authenticated user's profile including " +
                    "email, display name, roles, and security posture metadata. " +
                    "Requires valid JWT authentication token.";
                return operation;
            })
            .Produces<ProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<
        Ok<ProfileResponse>,
        ProblemHttpResult>>
        HandleAsync(
            ClaimsPrincipal user,
            IAuthService authService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(ProfileEndpoint));
        // Extract user ID from JWT claims
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning(
                "Profile request with invalid user ID claim: {Claim}",
                userIdClaim ?? "[null]");

            return TypedResults.Problem(
                title: "Invalid user identity",
                detail: "User ID in token is invalid.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Create command and call service
        var command = new GetUserProfileCommand(userId);
        var result = await authService.GetUserProfileAsync(command, cancellationToken);

        // Handle errors
        if (!result.Success)
        {
            return result.Error switch
            {
                ProfileError.UserNotFound => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to retrieve user profile. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                ProfileError.IdentityStoreUnavailable => TypedResults.Problem(
                    title: "Service temporarily unavailable",
                    detail: "Unable to retrieve user profile. Please try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable),

                _ => TypedResults.Problem(
                    title: "Profile retrieval failed",
                    detail: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        // Success - map to response DTO
        var response = new ProfileResponse(
            result.UserId,
            result.Email!,
            result.DisplayName!,
            result.Roles,
            result.MustChangePassword,
            result.CreatedAt);

        return TypedResults.Ok(response);
    }
}
