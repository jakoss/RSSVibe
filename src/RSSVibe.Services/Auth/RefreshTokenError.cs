namespace RSSVibe.Services.Auth;

/// <summary>
/// Error codes for refresh token operation.
/// </summary>
public enum RefreshTokenError
{
    /// <summary>
    /// Token not found, expired, or revoked.
    /// </summary>
    TokenInvalid,

    /// <summary>
    /// Token has already been used - replay attack detected.
    /// All user tokens have been revoked for security.
    /// </summary>
    TokenReplayDetected,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    ServiceUnavailable
}
