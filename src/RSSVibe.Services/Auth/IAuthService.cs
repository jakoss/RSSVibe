namespace RSSVibe.Services.Auth;

/// <summary>
/// Service for authentication and user registration operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user with the provided credentials and profile information.
    /// </summary>
    /// <param name="command">Registration command containing email, password, display name, and password change flag.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing user details on success or error information on failure.</returns>
    Task<RegisterUserResult> RegisterUserAsync(RegisterUserCommand command, CancellationToken cancellationToken = default);
}
