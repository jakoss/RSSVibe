namespace RSSVibe.Services.Auth;

/// <summary>
/// Errors that can occur during user profile retrieval.
/// </summary>
public enum ProfileError
{
    /// <summary>
    /// User not found in the identity store (data inconsistency).
    /// </summary>
    UserNotFound,

    /// <summary>
    /// Identity store (database) is unavailable or unreachable.
    /// </summary>
    IdentityStoreUnavailable
}
