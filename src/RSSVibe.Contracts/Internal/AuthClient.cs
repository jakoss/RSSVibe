using RSSVibe.Contracts.Auth;
using System.Net.Http.Json;

namespace RSSVibe.Contracts.Internal;

internal sealed class AuthClient(HttpClient httpClient) : IAuthClient
{
    private const string BaseRoute = "/api/v1/auth";

    public async Task<ApiResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{BaseRoute}/register",
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<RegisterResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{BaseRoute}/login",
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<LoginResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<RefreshTokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{BaseRoute}/refresh",
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<RefreshTokenResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<ProfileResponse>> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BaseRoute}/profile",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<ProfileResponse>(response, cancellationToken);
    }

    public async Task<ApiResultNoData> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{BaseRoute}/change-password",
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseNoDataAsync(response, cancellationToken);
    }
}
