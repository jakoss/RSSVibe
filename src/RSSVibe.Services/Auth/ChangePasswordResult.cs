namespace RSSVibe.Services.Auth;

/// <summary>
/// Result of password change operation.
/// </summary>
public sealed record ChangePasswordResult
{
    /// <summary>
    /// Indicates whether the password change was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error details if the operation failed.
    /// </summary>
    public ChangePasswordError? Error { get; init; }
}