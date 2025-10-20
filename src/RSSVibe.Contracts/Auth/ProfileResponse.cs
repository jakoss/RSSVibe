namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Current user profile response. Maps from ApplicationUser entity.
/// </summary>
public sealed record ProfileResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool MustChangePassword,
    DateTimeOffset CreatedAt
);
