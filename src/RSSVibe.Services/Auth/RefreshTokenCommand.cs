namespace RSSVibe.Services.Auth;

/// <summary>
/// Command to refresh access token using refresh token.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken
);
