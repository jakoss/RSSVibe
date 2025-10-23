namespace RSSVibe.ApiService.Configuration;

/// <summary>
/// Configuration options for authentication and user registration.
/// </summary>
public sealed class AuthConfiguration
{
    /// <summary>
    /// Gets or sets whether user registration is enabled in this environment.
    /// Typically disabled in production when root user provisioning is enabled.
    /// </summary>
    public bool AllowRegistration { get; init; }

    /// <summary>
    /// Gets or sets the email address of the root user (optional, for production provisioning).
    /// </summary>
    public string? RootUserEmail { get; init; }

    /// <summary>
    /// Gets or sets the password for the root user (optional, for production provisioning).
    /// </summary>
    public string? RootUserPassword { get; init; }
}
