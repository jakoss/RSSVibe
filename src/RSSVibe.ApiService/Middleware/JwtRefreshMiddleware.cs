using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using RSSVibe.Services.Auth;

namespace RSSVibe.ApiService.Middleware;

/// <summary>
/// Middleware that automatically refreshes expired JWT tokens using the refresh token from cookies.
/// Runs BEFORE authentication so expired tokens are refreshed transparently before endpoint processing.
/// </summary>
public sealed class JwtRefreshMiddleware(
    RequestDelegate next,
    ILogger<JwtRefreshMiddleware> logger)
{
    private static readonly JwtSecurityTokenHandler _tokenHandler = new();

    public async Task InvokeAsync(
        HttpContext context,
        IAuthService authService,
        IOptions<JwtConfiguration> jwtConfig)
    {
        // Try to refresh expired tokens before authentication
        await TryRefreshExpiredTokenAsync(context, authService, jwtConfig.Value, logger);

        await next(context);
    }

    /// <summary>
    /// Checks if the access token in the cookie is expired and refreshes it if a valid refresh token exists.
    /// This happens BEFORE authentication, so the request continues with fresh tokens transparently.
    /// </summary>
    private static async Task TryRefreshExpiredTokenAsync(
        HttpContext context,
        IAuthService authService,
        JwtConfiguration jwtConfig,
        ILogger<JwtRefreshMiddleware> logger)
    {
        // First, check if we even have a refresh token - if not, can't refresh anything
        if (!context.Request.Cookies.TryGetValue("refresh_token", out var refreshTokenString))
        {
            logger.LogDebug("No refresh_token cookie found");
            return;
        }

        // Check if access token exists and is still valid
        var accessTokenExpired = true;
        if (TryGetAccessToken(context, out var accessTokenString))
        {
            // Try to parse the token WITHOUT validation to check expiration
            try
            {
                var jwtToken = _tokenHandler.ReadJwtToken(accessTokenString);
                if (jwtToken.ValidTo > DateTime.UtcNow)
                {
                    // Token is still valid, no refresh needed
                    accessTokenExpired = false;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to parse JWT token");
                // Assume token is expired and proceed with refresh
            }
        }

        // If access token is still valid, no need to refresh
        if (!accessTokenExpired)
        {
            return;
        }

        logger.LogInformation("Access token missing or expired, attempting to refresh using refresh_token");

        try
        {
            // Call refresh endpoint using the refresh token
            var command = new RefreshTokenCommand(refreshTokenString);
            var result = await authService.RefreshTokenAsync(command, CancellationToken.None);

            if (!result.Success)
            {
                logger.LogInformation("Token refresh failed: {Error}", result.Error);
                return;
            }

            // Refresh successful - set new tokens in response cookies
            var cookieConfig = jwtConfig.Cookie;
            var cookieDomain = cookieConfig.Domain ?? context.Request.Host.Host;

            var accessTokenCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieConfig.RequireSecure,
                SameSite = cookieConfig.SameSite,
                Domain = cookieDomain,
                Expires = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds)
            };

            context.Response.Cookies.Append("access_token", result.AccessToken!, accessTokenCookieOptions);

            // CRITICAL: Also inject the new token into the current request's Authorization header
            // This ensures the current request can authenticate immediately without waiting for the response
            // to be sent and the browser to receive the new cookie from the Set-Cookie header
            context.Request.Headers["Authorization"] = $"Bearer {result.AccessToken}";

            // Update refresh token cookie
            var refreshTokenCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieConfig.RequireSecure,
                SameSite = cookieConfig.SameSite,
                Domain = cookieDomain,
                Expires = DateTimeOffset.UtcNow.AddDays(jwtConfig.RefreshTokenExpirationDays)
            };

            context.Response.Cookies.Append("refresh_token", result.RefreshToken!, refreshTokenCookieOptions);

            logger.LogInformation("Token refreshed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing token");
        }
    }

    /// <summary>
    /// Extracts the access token from either the Authorization header or the access_token cookie.
    /// </summary>
    private static bool TryGetAccessToken(HttpContext context, out string token)
    {
        token = string.Empty;

        // Try Authorization header first (Bearer token)
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Bearer ".Length..];
            return true;
        }

        // Try access_token cookie
        if (!context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
        {
            return false;
        }

        token = cookieToken;
        return true;

    }
}

/// <summary>
/// Extension method to register the JWT refresh middleware.
/// </summary>
public static class JwtRefreshMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtRefresh(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtRefreshMiddleware>();
    }
}
