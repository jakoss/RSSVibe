namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of user profile retrieval operation.
/// </summary>
public sealed record GetUserProfileResult
{
    public bool Success { get; init; }
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string[] Roles { get; init; } = [];
    public bool MustChangePassword { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public ProfileError? Error { get; init; }
}