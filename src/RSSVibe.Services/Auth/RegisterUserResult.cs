namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of a user registration attempt containing success status and user details.
/// </summary>
public sealed record RegisterUserResult
{
    /// <summary>
    /// Indicates if registration was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The newly created user's ID (populated on success).
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The newly created user's email (populated on success).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The newly created user's display name (populated on success).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Whether the user must change password on first login (populated on success).
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Error details if registration failed (populated on failure).
    /// </summary>
    public RegistrationError? Error { get; init; }
}
