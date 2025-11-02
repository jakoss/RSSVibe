namespace RSSVibe.Services.Auth;

/// <summary>
/// Possible errors during password change.
/// </summary>
public enum ChangePasswordError
{
    /// <summary>
    /// The current password provided is incorrect.
    /// </summary>
    InvalidCurrentPassword,

    /// <summary>
    /// The new password does not meet complexity requirements.
    /// </summary>
    WeakPassword,

    /// <summary>
    /// Database or Identity store is unavailable.
    /// </summary>
    IdentityStoreUnavailable
}
