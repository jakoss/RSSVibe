using Microsoft.AspNetCore.Identity;
using RSSVibe.Data.Entities;
using System.Data.Common;

namespace RSSVibe.Services.Auth;

/// <summary>
/// Implementation of authentication service for user registration and profile management.
/// </summary>
internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Registers a new user with email and password through ASP.NET Identity.
    /// </summary>
    public async Task<RegisterUserResult> RegisterUserAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        // Check if user already exists by normalized email
        var existingUser = await userManager.FindByEmailAsync(command.Email);
        if (existingUser is not null)
        {
            logger.LogInformation(
                "Registration attempt for existing email: {Email}",
                AnonymizeEmail(command.Email));

            return new RegisterUserResult
            {
                Success = false,
                Error = RegistrationError.EmailAlreadyExists
            };
        }

        // Create new user entity with required properties
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = command.Email,
            Email = command.Email,
            DisplayName = command.DisplayName,
            MustChangePassword = command.MustChangePassword
        };

        try
        {
            // Create user with password through Identity - handles hashing and validation
            var identityResult = await userManager.CreateAsync(user, command.Password);

            if (!identityResult.Succeeded)
            {
                var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
                logger.LogWarning(
                    "User creation failed for {Email}: {Errors}",
                    AnonymizeEmail(command.Email),
                    errors);

                // Password validation failures are handled by the validator in contracts
                // If we reach here, it's an unexpected error from UserManager
                return new RegisterUserResult
                {
                    Success = false,
                    Error = RegistrationError.InvalidPassword
                };
            }

            logger.LogInformation(
                "User registered successfully: {Email}",
                AnonymizeEmail(command.Email));

            return new RegisterUserResult
            {
                Success = true,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                MustChangePassword = user.MustChangePassword
            };
        }
        catch (DbException ex)
        {
            logger.LogError(
                ex,
                "Database error during user registration for {Email}",
                AnonymizeEmail(command.Email));

            return new RegisterUserResult
            {
                Success = false,
                Error = RegistrationError.IdentityStoreUnavailable
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error during user registration for {Email}",
                AnonymizeEmail(command.Email));

            return new RegisterUserResult
            {
                Success = false,
                Error = RegistrationError.IdentityStoreUnavailable
            };
        }
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
