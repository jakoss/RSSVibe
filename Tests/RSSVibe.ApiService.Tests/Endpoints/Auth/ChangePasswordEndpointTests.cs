using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.ApiService.Tests.Infrastructure;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the ChangePasswordEndpoint (/api/v1/auth/change-password).
/// Tests use real WebApplicationFactory with database and services.
/// </summary>
public class ChangePasswordEndpointTests : TestsBase
{
    [Test]
    public async Task ChangePasswordEndpoint_WithValidRequest_ShouldReturn204AndUpdatePassword()
    {
        // Arrange - Register and login user
        var client = WebApplicationFactory.CreateClient();
        var registerRequest = new RegisterRequest(
            Email: $"test_{Guid.CreateVersion7():N}@example.com",
            Password: "OldPass123!Secure",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var loginRequest = new LoginRequest(
            Email: registerRequest.Email,
            Password: "OldPass123!Secure",
            RememberMe: false
        );

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginData).IsNotNull();

        // Create authenticated client
        client = CreateAuthenticatedClient(loginData.AccessToken);

        var changeRequest = new ChangePasswordRequest(
            CurrentPassword: "OldPass123!Secure",
            NewPassword: "NewPass456!MoreSecure"
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", changeRequest);

        // Assert - HTTP response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert - Can login with new password
        var newLoginRequest = new LoginRequest(
            Email: registerRequest.Email,
            Password: "NewPass456!MoreSecure",
            RememberMe: false
        );

        var newLoginResponse = await WebApplicationFactory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", newLoginRequest);
        await Assert.That(newLoginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Assert - Cannot login with old password
        var oldLoginRequest = new LoginRequest(
            Email: registerRequest.Email,
            Password: "OldPass123!Secure",
            RememberMe: false
        );

        var oldLoginResponse = await WebApplicationFactory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", oldLoginRequest);
        await Assert.That(oldLoginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePasswordEndpoint_WithInvalidCurrentPassword_ShouldReturn401()
    {
        // Arrange - Create authenticated client
        var client = CreateAuthenticatedClient();

        var request = new ChangePasswordRequest(
            CurrentPassword: "WrongPassword123!",
            NewPassword: "NewPass456!Valid"
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problemDetails).IsNotNull();
        await Assert.That(problemDetails.Title).IsEqualTo("Authentication failed");
        await Assert.That(problemDetails.Detail).IsEqualTo("Current password is incorrect.");
    }

    [Test]
    public async Task ChangePasswordEndpoint_WithWeakNewPassword_ShouldReturn400()
    {
        // Arrange - Create authenticated client
        var client = CreateAuthenticatedClient();

        var request = new ChangePasswordRequest(
            CurrentPassword: TestApplication.TestUserPassword,
            NewPassword: "weak" // Too short, missing requirements
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChangePasswordEndpoint_WithNewPasswordSameAsCurrent_ShouldReturn400()
    {
        // Arrange - Create authenticated client
        var client = CreateAuthenticatedClient();

        var request = new ChangePasswordRequest(
            CurrentPassword: TestApplication.TestUserPassword,
            NewPassword: TestApplication.TestUserPassword // Same as current
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChangePasswordEndpoint_WithoutAuthentication_ShouldReturn401()
    {
        // Arrange - Use unauthenticated client
        var client = WebApplicationFactory.CreateClient();

        var request = new ChangePasswordRequest(
            CurrentPassword: "SomePassword123!",
            NewPassword: "NewPassword456!"
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePasswordEndpoint_ShouldRevokeRefreshTokens()
    {
        // Arrange - Register user and get refresh token
        var client = WebApplicationFactory.CreateClient();
        var registerRequest = new RegisterRequest(
            Email: $"revoke_test_{Guid.CreateVersion7():N}@example.com",
            Password: "OldPass123!Secure",
            DisplayName: "Revoke Test User",
            MustChangePassword: false
        );

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var loginRequest = new LoginRequest(
            Email: registerRequest.Email,
            Password: "OldPass123!Secure",
            RememberMe: true // Get refresh token
        );

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginData).IsNotNull();

        // Create authenticated client and change password
        var authClient = CreateAuthenticatedClient(loginData.AccessToken);
        var changeRequest = new ChangePasswordRequest(
            CurrentPassword: "OldPass123!Secure",
            NewPassword: "NewPass456!MoreSecure"
        );

        var changeResponse = await authClient.PostAsJsonAsync("/api/v1/auth/change-password", changeRequest);
        await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Act - Try to refresh token (should fail)
        var refreshRequest = new RefreshTokenRequest(RefreshToken: loginData.RefreshToken);
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert - Refresh should fail because tokens were revoked
        await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePasswordEndpoint_ShouldClearMustChangePasswordFlag()
    {
        // Arrange - Register user with MustChangePassword = true
        var client = WebApplicationFactory.CreateClient();
        var registerRequest = new RegisterRequest(
            Email: $"mustchange_test_{Guid.CreateVersion7():N}@example.com",
            Password: "TempPass123!Temp",
            DisplayName: "Must Change Test User",
            MustChangePassword: true
        );

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var registerData = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(registerData).IsNotNull();
        await Assert.That(registerData.MustChangePassword).IsTrue();

        // Login and change password
        var loginRequest = new LoginRequest(
            Email: registerRequest.Email,
            Password: "TempPass123!Temp",
            RememberMe: false
        );

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginData).IsNotNull();
        await Assert.That(loginData.MustChangePassword).IsTrue(); // Should still be true

        // Create authenticated client and change password
        var authClient = CreateAuthenticatedClient(loginData.AccessToken);
        var changeRequest = new ChangePasswordRequest(
            CurrentPassword: "TempPass123!Temp",
            NewPassword: "NewPass456!Permanent"
        );

        var changeResponse = await authClient.PostAsJsonAsync("/api/v1/auth/change-password", changeRequest);
        await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert - MustChangePassword flag should be cleared
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == registerData.UserId);
        await Assert.That(user).IsNotNull();
        await Assert.That(user.MustChangePassword).IsFalse();
    }
}
