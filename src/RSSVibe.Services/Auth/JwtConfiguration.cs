namespace RSSVibe.Services.Auth;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// </summary>
public sealed class JwtConfiguration
{
    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens.
    /// Must be at least 32 characters for HS256 algorithm.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Gets or sets the issuer claim for generated tokens (typically the API URL).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets or sets the audience claim for generated tokens (typically the client app).
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Gets or sets the access token expiration time in minutes.
    /// Default is 15 minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; init; } = 15;

    /// <summary>
    /// Gets or sets the refresh token expiration time in days.
    /// Default is 30 days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; init; } = 30;
}
