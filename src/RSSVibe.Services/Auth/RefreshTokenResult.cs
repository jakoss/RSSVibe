namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of refresh token operation.
/// </summary>
public sealed record RefreshTokenResult
{
    public bool Success { get; init; }
    public RefreshTokenError? Error { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresInSeconds { get; init; }
    public bool MustChangePassword { get; init; }
}
