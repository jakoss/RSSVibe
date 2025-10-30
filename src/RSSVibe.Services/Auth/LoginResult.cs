namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of login operation containing tokens or error information.
/// </summary>
public sealed record LoginResult
{
    /// <summary>
    /// Indicates if login was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// JWT access token. Null if login failed.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Refresh token string. Null if login failed.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Access token expiration time in seconds. Zero if login failed.
    /// </summary>
    public int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Flag indicating user must change password before accessing protected resources.
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Error code if login failed. Null if successful.
    /// </summary>
    public LoginError? Error { get; init; }
}
