using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;
using RSSVibe.Data.Entities;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the LoginEndpoint (/api/v1/auth/login).
/// Tests authentication, lockout, validation, and token generation.
/// </summary>
public class LoginEndpointTests : TestsBase
{
    [Test]
    public async Task LoginEndpoint_WithValidCredentials_ShouldReturn200WithTokens()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // First register a user
        var registerRequest = new RegisterRequest(
            Email: "login_test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Login Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Now login
        var loginRequest = new LoginRequest(
            Email: "login_test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(200);

        var loginResponse = result.Data!;
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse.AccessToken).IsNotEmpty();
        await Assert.That(loginResponse.RefreshToken).IsNotEmpty();
        await Assert.That(loginResponse.ExpiresInSeconds).IsGreaterThan(0);
        await Assert.That(loginResponse.MustChangePassword).IsEqualTo(false);

        // Assert - Database state
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == loginResponse.RefreshToken);

        await Assert.That(refreshToken).IsNotNull();
        await Assert.That(refreshToken.ExpiresAt).IsGreaterThan(DateTime.UtcNow);
        await Assert.That(refreshToken.IsUsed).IsFalse();
        await Assert.That(refreshToken.RevokedAt).IsNull();
    }

    [Test]
    public async Task LoginEndpoint_WithInvalidEmail_ShouldReturn400()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var loginRequest = new LoginRequest(
            Email: "invalid-email",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task LoginEndpoint_WithMissingPassword_ShouldReturn400()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var loginRequest = new LoginRequest(
            Email: "test@rssvibe.local",
            Password: "",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task LoginEndpoint_WithNonExistentUser_ShouldReturn401()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var loginRequest = new LoginRequest(
            Email: "nonexistent@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(401);
        await Assert.That(result.ErrorDetail).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginEndpoint_WithWrongPassword_ShouldReturn401()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "wrongpass_test@rssvibe.local",
            Password: "CorrectPassword123!",
            DisplayName: "Wrong Pass Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Try to login with wrong password
        var loginRequest = new LoginRequest(
            Email: "wrongpass_test@rssvibe.local",
            Password: "WrongPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(401);
        await Assert.That(result.ErrorDetail).Contains("Invalid email or password");

        // Assert - Database state (AccessFailedCount should increment)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("wrongpass_test@rssvibe.local");
        await Assert.That(user).IsNotNull();
        await Assert.That(user.AccessFailedCount).IsGreaterThan(0);
    }

    [Test]
    public async Task LoginEndpoint_WithLockedAccount_ShouldReturn423()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "locked_test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Locked Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Make 5 failed login attempts to trigger lockout
        var wrongLoginRequest = new LoginRequest(
            Email: "locked_test@rssvibe.local",
            Password: "WrongPassword123!",
            RememberMe: false
        );

        for (var i = 0; i < 5; i++)
        {
            await apiClient.Auth.LoginAsync(wrongLoginRequest);
        }

        // Now try with correct password (should be locked)
        var correctLoginRequest = new LoginRequest(
            Email: "locked_test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(correctLoginRequest);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(423);
        await Assert.That(result.ErrorDetail).Contains("locked");

        // Assert - Database state (user should be locked)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("locked_test@rssvibe.local");
        await Assert.That(user).IsNotNull();
        await Assert.That(user.LockoutEnd).IsNotNull();
        await Assert.That(user.LockoutEnd!.Value).IsGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task LoginEndpoint_WithMustChangePassword_ShouldReturnTrueFlag()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user with mustChangePassword flag
        var registerRequest = new RegisterRequest(
            Email: "mustchange_test@rssvibe.local",
            Password: "TemporaryPassword123!",
            DisplayName: "Must Change Test User",
            MustChangePassword: true
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Login
        var loginRequest = new LoginRequest(
            Email: "mustchange_test@rssvibe.local",
            Password: "TemporaryPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(200);

        var loginResponse = result.Data!;
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse.MustChangePassword).IsEqualTo(true);
    }

    [Test]
    public async Task LoginEndpoint_WithRememberMe_ShouldCreateRefreshToken()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "rememberme_test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Remember Me Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Login with rememberMe = true
        var loginRequest = new LoginRequest(
            Email: "rememberme_test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: true
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(200);

        var loginResponse = result.Data!;
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse.RefreshToken).IsNotEmpty();

        // Assert - Database state (refresh token should expire in ~7 days, configured in test)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == loginResponse.RefreshToken);

        await Assert.That(refreshToken).IsNotNull();
        await Assert.That(refreshToken.ExpiresAt).IsGreaterThan(DateTime.UtcNow.AddDays(6));
        await Assert.That(refreshToken.ExpiresAt).IsLessThan(DateTime.UtcNow.AddDays(8));
    }

    [Test]
    public async Task LoginEndpoint_SuccessfulLogin_ShouldResetFailureCount()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "resetfailure_test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "Reset Failure Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Make 3 failed login attempts
        var wrongLoginRequest = new LoginRequest(
            Email: "resetfailure_test@rssvibe.local",
            Password: "WrongPassword123!",
            RememberMe: false
        );

        for (var i = 0; i < 3; i++)
        {
            await apiClient.Auth.LoginAsync(wrongLoginRequest);
        }

        // Now login with correct password
        var correctLoginRequest = new LoginRequest(
            Email: "resetfailure_test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        var successResult = await apiClient.Auth.LoginAsync(correctLoginRequest);
        await Assert.That(successResult.IsSuccess).IsTrue();
        await Assert.That(successResult.StatusCode).IsEqualTo(200);

        // Assert - Database state (AccessFailedCount should be reset to 0)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("resetfailure_test@rssvibe.local");
        await Assert.That(user).IsNotNull();
        await Assert.That(user.AccessFailedCount).IsEqualTo(0);

        // Make 3 more failed attempts - should NOT be locked yet (failure count was reset)
        for (var i = 0; i < 3; i++)
        {
            var result = await apiClient.Auth.LoginAsync(wrongLoginRequest);
            // Should still return 401, not 423 (locked)
            await Assert.That(result.StatusCode).IsEqualTo(401);
        }
    }

    [Test]
    public async Task LoginEndpoint_WithoutRememberMe_ShouldCreateRefreshToken()
    {
        // Arrange
        var apiClient = CreateApiClient();

        // Register user
        var registerRequest = new RegisterRequest(
            Email: "noremember_test@rssvibe.local",
            Password: "ValidPassword123!",
            DisplayName: "No Remember Test User",
            MustChangePassword: false
        );
        await apiClient.Auth.RegisterAsync(registerRequest);

        // Login with rememberMe = false
        var loginRequest = new LoginRequest(
            Email: "noremember_test@rssvibe.local",
            Password: "ValidPassword123!",
            RememberMe: false
        );

        // Act
        var result = await apiClient.Auth.LoginAsync(loginRequest);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(200);

        var loginResponse = result.Data!;
        await Assert.That(loginResponse).IsNotNull();
        await Assert.That(loginResponse.RefreshToken).IsNotEmpty();

        // Assert - Database state (refresh token should expire in ~7 days, default expiration)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == loginResponse.RefreshToken);

        await Assert.That(refreshToken).IsNotNull();
        await Assert.That(refreshToken.ExpiresAt).IsGreaterThan(DateTime.UtcNow.AddDays(6));
        await Assert.That(refreshToken.ExpiresAt).IsLessThan(DateTime.UtcNow.AddDays(8));
    }
}
