using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.ApiService.Tests.Infrastructure;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Net;
using System.Net.Http.Json;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the RefreshEndpoint (/api/v1/auth/refresh).
/// Tests token rotation, replay detection, validation, and expiration.
/// </summary>
public class RefreshEndpointTests : TestsBase
{
    [Test]
    public async Task RefreshEndpoint_WithValidToken_ShouldReturn200WithNewTokens()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Register and login to get initial tokens
        var registerRequest = new RegisterRequest(
            Email: "refresh_valid@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Refresh Valid User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            Email: "refresh_valid@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // DEBUG: Check if login succeeded
        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            var loginContent = await loginResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Login Status: {loginResponse.StatusCode}");
            Console.WriteLine($"Login Content: {loginContent}");
        }
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        await Assert.That(login).IsNotNull();

        var refreshRequest = new RefreshTokenRequest(login.RefreshToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // DEBUG: Print response content if not OK
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response Status: {response.StatusCode}");
            Console.WriteLine($"Response Content: {content}");
        }

        // Assert - HTTP response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        await Assert.That(refreshResponse).IsNotNull();
        await Assert.That(refreshResponse.AccessToken).IsNotEmpty();
        await Assert.That(refreshResponse.RefreshToken).IsNotEmpty();
        await Assert.That(refreshResponse.ExpiresInSeconds).IsGreaterThan(0);
        await Assert.That(refreshResponse.MustChangePassword).IsEqualTo(false);

        // New tokens should be different from original
        await Assert.That(refreshResponse.AccessToken).IsNotEqualTo(login.AccessToken);
        await Assert.That(refreshResponse.RefreshToken).IsNotEqualTo(login.RefreshToken);

        // Assert - Database state
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        // Old token should be marked as used
        var oldToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == login.RefreshToken);
        await Assert.That(oldToken).IsNotNull();
        await Assert.That(oldToken.IsUsed).IsTrue();
        await Assert.That(oldToken.RevokedAt).IsNull();

        // New token should exist and not be used
        var newToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshResponse.RefreshToken);
        await Assert.That(newToken).IsNotNull();
        await Assert.That(newToken.IsUsed).IsFalse();
        await Assert.That(newToken.RevokedAt).IsNull();
        await Assert.That(newToken.ExpiresAt).IsGreaterThan(DateTime.UtcNow);
    }

    [Test]
    public async Task RefreshEndpoint_WithExpiredToken_ShouldReturn401()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Create an expired refresh token directly in the database
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == TestApplication.TestUserEmail);
        await Assert.That(user).IsNotNull();

        var expiredToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Token = "expired-token-" + Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            IsUsed = false
        };

        dbContext.RefreshTokens.Add(expiredToken);
        await dbContext.SaveChangesAsync();

        var refreshRequest = new RefreshTokenRequest(expiredToken.Token);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails.Detail).Contains("invalid, expired, or has been revoked");
    }

    [Test]
    public async Task RefreshEndpoint_WithRevokedToken_ShouldReturn401()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Create a revoked refresh token directly in the database
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == TestApplication.TestUserEmail);
        await Assert.That(user).IsNotNull();

        var revokedToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Token = "revoked-token-" + Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false,
            RevokedAt = DateTime.UtcNow.AddHours(-1) // Revoked 1 hour ago
        };

        dbContext.RefreshTokens.Add(revokedToken);
        await dbContext.SaveChangesAsync();

        var refreshRequest = new RefreshTokenRequest(revokedToken.Token);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails.Detail).Contains("invalid, expired, or has been revoked");
    }

    [Test]
    public async Task RefreshEndpoint_WithNonExistentToken_ShouldReturn401()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var refreshRequest = new RefreshTokenRequest("non-existent-token-" + Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails.Detail).Contains("invalid, expired, or has been revoked");
    }

    [Test]
    public async Task RefreshEndpoint_WithUsedToken_ShouldReturn409AndRevokeAllTokens()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Register and login to get initial tokens
        var registerRequest = new RegisterRequest(
            Email: "refresh_replay@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Refresh Replay User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            Email: "refresh_replay@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        await Assert.That(login).IsNotNull();
        var refreshRequest = new RefreshTokenRequest(login.RefreshToken);

        // Use the token once (successfully)
        var firstResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act - Try to use the same token again (replay attack)
        var replayResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert - HTTP response
        await Assert.That(replayResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);

        var problemDetails = await replayResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails.Detail).Contains("already been used");
        await Assert.That(problemDetails.Detail).Contains("revoked");

        // Assert - Database state (all user tokens should be revoked)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "refresh_replay@rssvibe.local");
        await Assert.That(user).IsNotNull();

        var userTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();

        await Assert.That(userTokens.Count).IsGreaterThan(0);

        // All tokens should be revoked
        foreach (var token in userTokens)
        {
            await Assert.That(token.RevokedAt).IsNotNull();
            await Assert.That(token.RevokedAt!.Value).IsLessThanOrEqualTo(DateTime.UtcNow);
        }
    }

    [Test]
    public async Task RefreshEndpoint_WithEmptyToken_ShouldReturn400()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var refreshRequest = new RefreshTokenRequest("");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RefreshEndpoint_PreservesMustChangePasswordFlag()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Register user with MustChangePassword = true
        var registerRequest = new RegisterRequest(
            Email: "refresh_mustchange@rssvibe.local",
            Password: "TemporaryPassword123!",
            DisplayName: "Refresh MustChange User",
            MustChangePassword: true
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            Email: "refresh_mustchange@rssvibe.local",
            Password: "TemporaryPassword123!",
            RememberMe: true
        );
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        await Assert.That(login).IsNotNull();
        await Assert.That(login.MustChangePassword).IsEqualTo(true);

        var refreshRequest = new RefreshTokenRequest(login.RefreshToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        await Assert.That(refreshResponse).IsNotNull();
        await Assert.That(refreshResponse.MustChangePassword).IsEqualTo(true);
    }

    [Test]
    public async Task RefreshEndpoint_VerifiesSlidingExpiration()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Register and login to get initial tokens
        var registerRequest = new RegisterRequest(
            Email: "refresh_sliding@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Refresh Sliding User",
            MustChangePassword: false
        );
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            Email: "refresh_sliding@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(login).IsNotNull();

        // Check original token expiration
        await using var scope1 = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext1 = scope1.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var originalToken = await dbContext1.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == login.RefreshToken);
        await Assert.That(originalToken).IsNotNull();

        var originalLifetime = (originalToken.ExpiresAt - originalToken.CreatedAt).TotalDays;

        var refreshRequest = new RefreshTokenRequest(login.RefreshToken);

        // Act - Refresh the token
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        await Assert.That(refreshResponse).IsNotNull();

        // Check new token expiration (should have same lifetime as original)
        await using var scope2 = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var newToken = await dbContext2.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshResponse.RefreshToken);
        await Assert.That(newToken).IsNotNull();

        var newLifetime = (newToken.ExpiresAt - newToken.CreatedAt).TotalDays;

        // New token should have same lifetime as original (sliding window)
        // Allow 1 second tolerance for test execution time
        await Assert.That(Math.Abs(newLifetime - originalLifetime)).IsLessThan(1.0 / 86400); // 1 second in days

        // New token should expire later than original (since it was created later)
        await Assert.That(newToken.ExpiresAt).IsGreaterThan(originalToken.ExpiresAt);
    }
}
