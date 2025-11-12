using Microsoft.AspNetCore.Components.Authorization;
using RSSVibe.Contracts;

namespace RSSVibe.Client.Services;

/// <summary>
/// Provides access tokens for WebAssembly client API calls.
/// Retrieves tokens from AuthenticationStateProvider.
/// </summary>
public sealed class ClientAccessTokenProvider(AuthenticationStateProvider authStateProvider) : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Get token from claims (adjust claim type based on your auth implementation)
        return user.FindFirst("access_token")?.Value;
    }
}
