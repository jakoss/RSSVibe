namespace RSSVibe.Services.Auth;

/// <summary>
/// Command for retrieving user profile by user ID.
/// </summary>
public sealed record GetUserProfileCommand(
    Guid UserId
);
