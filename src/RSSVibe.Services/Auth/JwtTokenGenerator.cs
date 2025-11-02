using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RSSVibe.Data.Entities;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RSSVibe.Services.Auth;

/// <summary>
/// Implementation of JWT token generation using HS256 algorithm.
/// </summary>
internal sealed class JwtTokenGenerator(
    IOptions<JwtConfiguration> jwtConfig) : IJwtTokenGenerator
{
    /// <inheritdoc />
    public (string Token, int ExpiresInSeconds) GenerateAccessToken(ApplicationUser user)
    {
        var config = jwtConfig.Value;
        var expiresInSeconds = config.AccessTokenExpirationMinutes * 60;
        var expiration = DateTime.UtcNow.AddMinutes(config.AccessTokenExpirationMinutes);

        var claims = new Dictionary<string, object>
        {
            { JwtRegisteredClaimNames.Sub, user.Id.ToString() },
            { JwtRegisteredClaimNames.Email, user.Email! },
            { JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString() },
            { "display_name", user.DisplayName },
            { "must_change_password", user.MustChangePassword.ToString() }
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims.Select(c => new Claim(c.Key, c.Value.ToString()!))),
            Expires = expiration,
            Issuer = config.Issuer,
            Audience = config.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JsonWebTokenHandler();
        var tokenString = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenString, expiresInSeconds);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
