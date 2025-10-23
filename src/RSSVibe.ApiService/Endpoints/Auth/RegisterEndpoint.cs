using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using RSSVibe.ApiService.Configuration;
using RSSVibe.Contracts.Auth;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Endpoints.Auth;

/// <summary>
/// Endpoint for user registration with email and password.
/// </summary>
public static class RegisterEndpoint
{
    /// <summary>
    /// Maps the POST /auth/register endpoint to the handler within an auth group.
    /// </summary>
    public static RouteGroupBuilder MapRegisterEndpoint(
        this RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("Register")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Register a new user account";
                operation.Description = "Creates a new user account using email and password. " +
                                       "Disabled in production when root user provisioning is enabled.";
                return operation;
            });

        return group;
    }

    /// <summary>
    /// Handles the registration request with comprehensive validation and error handling.
    /// </summary>
    private static async Task<Results<
        Created<RegisterResponse>,
        ValidationProblem,
        ProblemHttpResult,
        ForbidHttpResult>>
        HandleAsync(
            RegisterRequest request,
            IAuthService authService,
            IOptions<AuthConfiguration> config,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RegisterEndpoint");

        // Check if registration is enabled in this environment (fail-fast for production)
        if (!config.Value.AllowRegistration)
        {
            logger.LogWarning(
                "Registration attempt rejected (disabled in environment)");

            return TypedResults.Problem(
                title: "Registration is disabled",
                detail: "User registration is not available in this environment.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // Map request to service command
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            request.MustChangePassword);

        // Invoke service layer
        var result = await authService.RegisterUserAsync(command, cancellationToken);

        // Handle service result
        if (!result.Success)
        {
            return result.Error switch
            {
                RegistrationError.EmailAlreadyExists =>
                    TypedResults.Problem(
                        title: "Email address is already registered",
                        detail: "An account with this email address already exists.",
                        statusCode: StatusCodes.Status409Conflict),

                RegistrationError.IdentityStoreUnavailable =>
                    TypedResults.Problem(
                        title: "Service temporarily unavailable",
                        detail: "Unable to connect to the identity store. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                _ =>
                    TypedResults.Problem(
                        title: "Registration failed",
                        detail: "An unexpected error occurred during registration.",
                        statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // Construct success response with created user data
        var response = new RegisterResponse(
            result.UserId,
            result.Email!,
            result.DisplayName!,
            result.MustChangePassword);

        logger.LogInformation(
            "User registered successfully: {Email}",
            AnonymizeEmail(result.Email));

        // Return 201 Created with Location header pointing to user profile
        return TypedResults.Created("/api/v1/auth/profile", response);
    }

    /// <summary>
    /// Anonymizes an email address for logging purposes (e.g., "u***@example.com").
    /// </summary>
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
