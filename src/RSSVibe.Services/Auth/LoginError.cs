namespace RSSVibe.Services.Auth;

/// <summary>
/// Error codes for login operation failures.
/// </summary>
public enum LoginError
{
    /// <summary>
    /// No error occurred (successful login).
    /// </summary>
    None,

    /// <summary>
    /// Email or password is incorrect.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Account is locked due to too many failed attempts.
    /// </summary>
    AccountLocked,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    IdentityStoreUnavailable
}
