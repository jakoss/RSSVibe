using Microsoft.AspNetCore.Http;

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

    /// <summary>
    /// Gets or sets the cookie configuration for token storage.
    /// </summary>
    public CookieConfiguration Cookie { get; init; } = new();
}

/// <summary>
/// Configuration options for JWT token storage in cookies.
/// </summary>
public sealed class CookieConfiguration
{
    /// <summary>
    /// Gets or sets the SameSite attribute for cookies.
    /// Default is Lax for better security while allowing same-site requests.
    /// Use None when API and client are on different domains.
    /// </summary>
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;

    /// <summary>
    /// Gets or sets whether the Secure flag should be required on cookies.
    /// When true, cookies are only sent over HTTPS.
    /// Default is true for production safety.
    /// </summary>
    public bool RequireSecure { get; init; } = true;

    /// <summary>
    /// Gets or sets the domain for the cookie. If null, the domain from the request is used.
    /// Set this if you want cookies shared across subdomains (e.g., ".example.com").
    /// Default is null (use request host).
    /// </summary>
    public string? Domain { get; init; }
}
