using Microsoft.AspNetCore.Components.Authorization;
using RSSVibe.Contracts;
using System.Security.Claims;

namespace RSSVibe.Client.Services;

/// <summary>
/// Custom authentication state provider that uses localStorage for persistence.
/// Avoids repeated /auth/profile calls on page refresh.
/// Auth state is cached locally and synced with server only when explicitly refreshed.
/// </summary>
public sealed class CustomAuthStateProvider(IServiceProvider serviceProvider) : AuthenticationStateProvider
{
    // Use Lazy to defer API client resolution until it's actually needed.
    // This breaks the circular dependency: CustomAuthStateProvider -> IRSSVibeApiClient -> AuthenticationHandler -> IAccessTokenProvider -> AuthenticationStateProvider
    private readonly Lazy<IRSSVibeApiClient> _apiClient = new(serviceProvider.GetRequiredService<IRSSVibeApiClient>);
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private const string AuthStorageKey = "rssvibe_auth_state";

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Return current auth state without loading from server.
        // This prevents circular dependencies during app initialization.
        // Auth state is loaded explicitly via RestoreAuthStateAsync() on app startup.
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    /// <summary>
    /// Restores authentication state from localStorage if available.
    /// Falls back to server if no cached state exists.
    /// This avoids repeated /auth/profile calls on every page refresh.
    /// </summary>
    public async Task RestoreAuthStateAsync(Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        try
        {
            // Try to restore from localStorage first (fast, no server call)
            var cachedState = await localStorage.GetItemAsync<CachedAuthState>(AuthStorageKey);
            if (cachedState is not null)
            {
                if (!cachedState.IsExpired)
                {
                    System.Diagnostics.Debug.WriteLine($"[Auth] Restored from cache: {cachedState.Email}, expires in {cachedState.CacheDurationMinutes - (int)(DateTime.UtcNow - cachedState.CachedAt).TotalMinutes} min");
                    RestoreFromCachedState(cachedState);
                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Auth] Cache expired, loading from server");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Auth] No cache found, loading from server");
            }
        }
        catch (Exception ex)
        {
            // localStorage error - fall through to server sync
            System.Diagnostics.Debug.WriteLine($"[Auth] Cache restore error: {ex.Message}");
        }

        // No valid cached state, load from server
        await LoadAuthStateFromServerAsync(localStorage);
    }

    /// <summary>
    /// Loads authentication state from the server's /auth/profile endpoint.
    /// Used when no valid cached state exists.
    /// </summary>
    private async Task LoadAuthStateFromServerAsync(Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        try
        {
            var result = await _apiClient.Value.Auth.GetProfileAsync();

            if (result is { IsSuccess: true, Data: not null })
            {
                var profile = result.Data;
                var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, profile.UserId.ToString()),
                    new Claim(ClaimTypes.Name, profile.DisplayName),
                    new Claim(ClaimTypes.Email, profile.Email),
                    new Claim("MustChangePassword", profile.MustChangePassword.ToString()),
                    ..profile.Roles.Select(role => new Claim(ClaimTypes.Role, role))
                ], "apiauth");

                _currentUser = new ClaimsPrincipal(identity);

                // Cache the state locally
                var cachedState = new CachedAuthState
                {
                    UserId = profile.UserId.ToString(),
                    DisplayName = profile.DisplayName,
                    Email = profile.Email,
                    MustChangePassword = profile.MustChangePassword,
                    Roles = profile.Roles.ToList(),
                    CachedAt = DateTime.UtcNow,
                    CacheDurationMinutes = 60
                };

                try
                {
                    await localStorage.SetItemAsync(AuthStorageKey, cachedState);
                    System.Diagnostics.Debug.WriteLine($"[Auth] Cached auth state for {profile.Email}");
                }
                catch (Exception ex)
                {
                    // Cache write failed, but auth state is still valid
                    // Log the error for debugging
                    System.Diagnostics.Debug.WriteLine($"[Auth] Failed to cache auth state: {ex.Message} (Type: {ex.GetType().Name})");
                }
            }
            else
            {
                // Not authenticated
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                await localStorage.RemoveItemAsync(AuthStorageKey);
            }
        }
        catch
        {
            // Network error - try to restore from stale cache
            try
            {
                var staleCache = await localStorage.GetItemAsync<CachedAuthState>(AuthStorageKey);
                if (staleCache is not null)
                {
                    RestoreFromCachedState(staleCache);
                    return;
                }
            }
            catch { }

            // No cache available, user is unauthenticated
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    /// <summary>
    /// Restores user claims from a cached auth state.
    /// </summary>
    private void RestoreFromCachedState(CachedAuthState cached)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, cached.UserId),
            new Claim(ClaimTypes.Name, cached.DisplayName),
            new Claim(ClaimTypes.Email, cached.Email),
            new Claim("MustChangePassword", cached.MustChangePassword.ToString()),
            ..cached.Roles.Select(role => new Claim(ClaimTypes.Role, role))
        ], "apiauth");

        _currentUser = new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Refreshes authentication state from the server.
    /// Call this after login or to validate cached state is still current.
    /// </summary>
    public async Task RefreshAuthStateAsync(Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        await LoadAuthStateFromServerAsync(localStorage);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    /// <summary>
    /// Logs out the user by clearing both local state and cached storage.
    /// </summary>
    public async Task LogoutAsync(Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        try
        {
            await localStorage.RemoveItemAsync(AuthStorageKey);
        }
        catch { }
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
}

/// <summary>
/// Cached authentication state stored in localStorage.
/// </summary>
public sealed class CachedAuthState
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required bool MustChangePassword { get; init; }
    public required List<string> Roles { get; init; }
    public required DateTime CachedAt { get; init; }
    public required int CacheDurationMinutes { get; init; }

    public bool IsExpired => DateTime.UtcNow > CachedAt.AddMinutes(CacheDurationMinutes);
}
