namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for changing a user's password.
/// </summary>
/// <param name="UserId">ID of the user changing their password.</param>
/// <param name="CurrentPassword">Current password for verification.</param>
/// <param name="NewPassword">New password to set.</param>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);