namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Response from token refresh. Contains new JWT access and refresh tokens.
/// </summary>
public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    bool MustChangePassword
);
