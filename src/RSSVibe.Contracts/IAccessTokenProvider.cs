namespace RSSVibe.Contracts;

/// <summary>
/// Provides access tokens for HTTP requests.
/// Implement this interface to supply bearer tokens for authenticated API calls.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Gets the current access token for API authentication.
    /// </summary>
    /// <returns>The access token, or null if not authenticated.</returns>
    Task<string?> GetAccessTokenAsync();
}
