namespace RSSVibe.Services.Auth;

/// <summary>
/// Command to register a new user with email and password.
/// </summary>
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    bool MustChangePassword
);
