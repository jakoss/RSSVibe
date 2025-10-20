namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Response from user registration. Represents ApplicationUser entity data returned to client.
/// </summary>
public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool MustChangePassword
);
