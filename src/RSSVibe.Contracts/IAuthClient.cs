using RSSVibe.Contracts.Auth;

namespace RSSVibe.Contracts;

/// <summary>
/// Type-safe client for authentication endpoints.
/// </summary>
public interface IAuthClient
{
    /// <summary>
    /// POST /api/v1/auth/register - Register a new user account.
    /// </summary>
    Task<ApiResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/auth/login - Authenticate user credentials.
    /// </summary>
    Task<ApiResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/auth/refresh - Refresh JWT access token.
    /// </summary>
    Task<ApiResult<RefreshTokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/auth/profile - Get current user profile.
    /// </summary>
    Task<ApiResult<ProfileResponse>> GetProfileAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/auth/change-password - Change user password.
    /// </summary>
    Task<ApiResultNoData> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
