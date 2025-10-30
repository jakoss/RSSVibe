using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Data.Common;

namespace RSSVibe.Services.Auth;

/// <summary>
/// Implementation of authentication service for user registration and profile management.
/// </summary>
internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IJwtTokenGenerator jwtTokenGenerator,
    RssVibeDbContext dbContext,
    IOptions<JwtConfiguration> jwtConfig,
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

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find user by email (normalized by Identity)
            var user = await userManager.FindByEmailAsync(command.Email);
            if (user is null)
            {
                // Don't reveal that email doesn't exist (prevents enumeration)
                logger.LogInformation(
                    "Failed login attempt for non-existent email: {Email}",
                    AnonymizeEmail(command.Email));

                return new LoginResult
                {
                    Success = false,
                    Error = LoginError.InvalidCredentials
                };
            }

            // Check if account is locked out
            if (await userManager.IsLockedOutAsync(user))
            {
                logger.LogWarning(
                    "Login attempt for locked account: {Email}",
                    AnonymizeEmail(command.Email));

                return new LoginResult
                {
                    Success = false,
                    Error = LoginError.AccountLocked
                };
            }

            // Verify password using UserManager (handles constant-time comparison)
            var passwordValid = await userManager.CheckPasswordAsync(user, command.Password);

            if (!passwordValid)
            {
                // Increment failure count and check for lockout
                await userManager.AccessFailedAsync(user);

                logger.LogInformation(
                    "Failed login attempt for email: {Email}",
                    AnonymizeEmail(command.Email));

                return new LoginResult
                {
                    Success = false,
                    Error = LoginError.InvalidCredentials
                };
            }

            // Login successful - generate tokens

            // 1. Generate JWT access token
            var (accessToken, expiresInSeconds) = jwtTokenGenerator.GenerateAccessToken(user);

            // 2. Generate refresh token
            var refreshTokenString = jwtTokenGenerator.GenerateRefreshToken();

            // 3. Calculate expiration based on rememberMe flag
            var config = jwtConfig.Value;
            var refreshTokenExpiration = command.RememberMe
                ? DateTime.UtcNow.AddDays(config.RefreshTokenExpirationDays)
                : DateTime.UtcNow.AddDays(7); // Default 7 days without rememberMe

            // 4. Store refresh token in database
            var refreshToken = new RefreshToken
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                Token = refreshTokenString,
                ExpiresAt = refreshTokenExpiration,
                CreatedAt = DateTime.UtcNow,
                IsUsed = false
            };

            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // 5. Reset failure count on successful login
            await userManager.ResetAccessFailedCountAsync(user);

            logger.LogInformation(
                "Successful login for user: {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshTokenString,
                ExpiresInSeconds = expiresInSeconds,
                MustChangePassword = user.MustChangePassword
            };
        }
        catch (DbException ex)
        {
            logger.LogError(
                ex,
                "Database error during login for {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = false,
                Error = LoginError.IdentityStoreUnavailable
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error during login for {Email}",
                AnonymizeEmail(command.Email));

            return new LoginResult
            {
                Success = false,
                Error = LoginError.IdentityStoreUnavailable
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
