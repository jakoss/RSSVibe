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

    /// <summary>
    /// Authenticates user credentials and generates access and refresh tokens.
    /// </summary>
    /// <param name="command">Login command containing email, password, and remember me flag.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing tokens on success or error information on failure.</returns>
    Task<LoginResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes access token using refresh token with replay attack detection.
    /// </summary>
    /// <param name="command">Refresh token command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing new tokens or error information.</returns>
    Task<RefreshTokenResult> RefreshTokenAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default);
}
