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
            MustChangePassword = command.MustChangePassword,
            CreatedAt = DateTimeOffset.UtcNow
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

    /// <inheritdoc />
    public async Task<RefreshTokenResult> RefreshTokenAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Look up token (note: User navigation property won't be loaded due to AddIdentityCore)
            var refreshToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken);

            // 2. Validate token existence
            if (refreshToken is null)
            {
                logger.LogInformation("Refresh attempt with non-existent token");
                return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
            }

            // 3. Load user separately (AddIdentityCore doesn't configure EF navigation properties)
            // We query directly from DbContext instead of UserManager for better reliability
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);

            if (user is null)
            {
                logger.LogError(
                    "User {UserId} not found for refresh token {TokenId}",
                    refreshToken.UserId,
                    refreshToken.Id);
                return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
            }

            // 4. Validate token expiration
            if (refreshToken.ExpiresAt < DateTime.UtcNow)
            {
                logger.LogInformation("Refresh attempt with expired token for user: {UserId}", refreshToken.UserId);
                return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
            }

            // 5. Validate token not revoked
            if (refreshToken.RevokedAt is not null)
            {
                logger.LogInformation("Refresh attempt with revoked token for user: {UserId}", refreshToken.UserId);
                return new RefreshTokenResult { Success = false, Error = RefreshTokenError.TokenInvalid };
            }

            // 6. CRITICAL: Check for replay attack
            if (refreshToken.IsUsed)
            {
                logger.LogWarning(
                    "SECURITY: Token replay detected for user {UserId}. Revoking all tokens.",
                    refreshToken.UserId);

                // Revoke all user tokens
                await dbContext.RefreshTokens
                    .Where(rt => rt.UserId == refreshToken.UserId && rt.RevokedAt == null)
                    .ExecuteUpdateAsync(
                        rt => rt.SetProperty(x => x.RevokedAt, DateTime.UtcNow),
                        cancellationToken);

                return new RefreshTokenResult
                {
                    Success = false,
                    Error = RefreshTokenError.TokenReplayDetected
                };
            }

            // 7. Use execution strategy for PostgreSQL transaction handling with state to avoid closures
            // Note: ExecuteInTransactionAsync would be ideal but doesn't support returning values with Npgsql
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var state = (refreshToken, user, jwtTokenGenerator, dbContext, logger);

            return await strategy.ExecuteAsync(
                state,
                static async (state, ct) =>
                {
                    var (refreshToken, user, jwtTokenGenerator, dbContext, logger) = state;

                    await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

                    try
                    {
                        // 8. Mark old token as used
                        refreshToken.IsUsed = true;

                        // 9. Generate new access token
                        var (accessToken, expiresInSeconds) = jwtTokenGenerator.GenerateAccessToken(user);

                        // 10. Generate new refresh token
                        var newRefreshTokenString = jwtTokenGenerator.GenerateRefreshToken();

                        // 11. Calculate new refresh token expiration (sliding window)
                        var newExpiration = DateTime.UtcNow.AddDays(
                            (refreshToken.ExpiresAt - refreshToken.CreatedAt).TotalDays);

                        // 12. Create new refresh token entity
                        var newRefreshToken = new RefreshToken
                        {
                            Id = Guid.CreateVersion7(),
                            UserId = refreshToken.UserId,
                            Token = newRefreshTokenString,
                            ExpiresAt = newExpiration,
                            CreatedAt = DateTime.UtcNow,
                            IsUsed = false
                        };

                        dbContext.RefreshTokens.Add(newRefreshToken);

                        // 13. Save changes and commit transaction
                        await dbContext.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);

                        logger.LogInformation(
                            "Token refreshed successfully for user: {UserId}",
                            refreshToken.UserId);

                        // 14. Return success result
                        return new RefreshTokenResult
                        {
                            Success = true,
                            AccessToken = accessToken,
                            RefreshToken = newRefreshTokenString,
                            ExpiresInSeconds = expiresInSeconds,
                            MustChangePassword = user.MustChangePassword
                        };
                    }
                    catch
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                },
                cancellationToken);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Database error during token refresh");
            return new RefreshTokenResult
            {
                Success = false,
                Error = RefreshTokenError.ServiceUnavailable
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during token refresh");
            return new RefreshTokenResult
            {
                Success = false,
                Error = RefreshTokenError.ServiceUnavailable
            };
        }
    }

    /// <inheritdoc />
    public async Task<ChangePasswordResult> ChangePasswordAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // a. Find user by ID using UserManager.FindByIdAsync(userId)
            var user = await userManager.FindByIdAsync(command.UserId.ToString());
            if (user is null)
            {
                // Don't reveal that user doesn't exist (prevents enumeration)
                logger.LogWarning(
                    "Failed password change attempt for non-existent user: {UserId}",
                    command.UserId);

                return new ChangePasswordResult
                {
                    Success = false,
                    Error = ChangePasswordError.InvalidCurrentPassword
                };
            }

            // b. Verify current password using UserManager.CheckPasswordAsync(user, currentPassword)
            var passwordValid = await userManager.CheckPasswordAsync(user, command.CurrentPassword);
            if (!passwordValid)
            {
                logger.LogWarning(
                    "Failed password change attempt for user {UserId}: Invalid current password",
                    command.UserId);

                return new ChangePasswordResult
                {
                    Success = false,
                    Error = ChangePasswordError.InvalidCurrentPassword
                };
            }

            // c. Use execution strategy for PostgreSQL transaction handling
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var state = (command, user, userManager, dbContext, logger);

            return await strategy.ExecuteAsync(
                state,
                static async (state, ct) =>
                {
                    var (command, user, userManager, dbContext, logger) = state;

                    await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

                    try
                    {
                        // c. Change password using UserManager.ChangePasswordAsync(user, currentPassword, newPassword)
                        var identityResult = await userManager.ChangePasswordAsync(
                            user,
                            command.CurrentPassword,
                            command.NewPassword);

                        if (!identityResult.Succeeded)
                        {
                            var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
                            logger.LogWarning(
                                "Password change failed for user {UserId}: {Errors}",
                                command.UserId,
                                errors);

                            return new ChangePasswordResult
                            {
                                Success = false,
                                Error = ChangePasswordError.WeakPassword
                            };
                        }

                        // d. Clear MustChangePassword flag
                        user.MustChangePassword = false;
                        var updateResult = await userManager.UpdateAsync(user);

                        if (!updateResult.Succeeded)
                        {
                            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                            logger.LogError(
                                "Failed to clear MustChangePassword flag for user {UserId}: {Errors}",
                                command.UserId,
                                errors);

                            return new ChangePasswordResult
                            {
                                Success = false,
                                Error = ChangePasswordError.IdentityStoreUnavailable
                            };
                        }

                        // e. Revoke all active refresh tokens
                        await dbContext.RefreshTokens
                            .Where(rt => rt.UserId == command.UserId && rt.RevokedAt == null)
                            .ExecuteUpdateAsync(
                                setters => setters.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow),
                                ct);

                        // f. Commit transaction
                        await dbContext.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);

                        logger.LogInformation(
                            "Password changed successfully for user: {UserId}",
                            command.UserId);

                        return new ChangePasswordResult { Success = true };
                    }
                    catch
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                },
                cancellationToken);
        }
        catch (DbException ex)
        {
            logger.LogError(
                ex,
                "Database error during password change for user {UserId}",
                command.UserId);

            return new ChangePasswordResult
            {
                Success = false,
                Error = ChangePasswordError.IdentityStoreUnavailable
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error during password change for user {UserId}",
                command.UserId);

            return new ChangePasswordResult
            {
                Success = false,
                Error = ChangePasswordError.IdentityStoreUnavailable
            };
        }
    }

    /// <inheritdoc />
    public async Task<GetUserProfileResult> GetUserProfileAsync(
        GetUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find user by ID from JWT claims
            var user = await userManager.FindByIdAsync(command.UserId.ToString());

            if (user is null)
            {
                // User in JWT but not in database - data inconsistency
                logger.LogWarning(
                    "User profile requested but user not found in database: {UserId}",
                    command.UserId);

                return new GetUserProfileResult
                {
                    Success = false,
                    Error = ProfileError.UserNotFound
                };
            }

            // Retrieve user roles from Identity
            var roles = await userManager.GetRolesAsync(user);

            logger.LogInformation(
                "User profile retrieved successfully for user: {UserId}",
                command.UserId);

            // Map entity to result
            return new GetUserProfileResult
            {
                Success = true,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Roles = [.. roles],
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt
            };
        }
        catch (DbException ex)
        {
            logger.LogError(
                ex,
                "Database error while retrieving user profile for user: {UserId}",
                command.UserId);

            return new GetUserProfileResult
            {
                Success = false,
                Error = ProfileError.IdentityStoreUnavailable
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error while retrieving user profile for user: {UserId}",
                command.UserId);

            return new GetUserProfileResult
            {
                Success = false,
                Error = ProfileError.IdentityStoreUnavailable
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
