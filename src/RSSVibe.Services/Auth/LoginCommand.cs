namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for user login operation.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe
);
