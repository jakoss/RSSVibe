using RSSVibe.Data.Entities;

namespace RSSVibe.Services.Auth;

/// <summary>
/// Service for generating JWT access and refresh tokens.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">The user to generate a token for.</param>
    /// <returns>A tuple containing the JWT token string and expiration time in seconds.</returns>
    (string Token, int ExpiresInSeconds) GenerateAccessToken(ApplicationUser user);

    /// <summary>
    /// Generates a refresh token for the specified user.
    /// </summary>
    /// <returns>A cryptographically secure random refresh token string.</returns>
    string GenerateRefreshToken();
}
