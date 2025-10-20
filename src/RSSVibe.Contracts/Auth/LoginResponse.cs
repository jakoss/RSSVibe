namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Response from authentication. Contains JWT access token and refresh token.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    bool MustChangePassword
);
