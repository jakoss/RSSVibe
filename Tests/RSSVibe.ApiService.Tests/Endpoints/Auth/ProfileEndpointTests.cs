using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data.Entities;
using RSSVibe.Services.Auth;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the ProfileEndpoint (/api/v1/auth/profile).
/// Tests use real WebApplicationFactory with database and services.
/// </summary>
public class ProfileEndpointTests : TestsBase
{
    [Test]
    public async Task ProfileEndpoint_WithValidToken_ShouldReturnUserProfile()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        // Act
        var result = await apiClient.Auth.GetProfileAsync();

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo((int)HttpStatusCode.OK);

        var profileData = result.Data!;
        await Assert.That(profileData).IsNotNull();
        await Assert.That(profileData.Email).IsEqualTo(Infrastructure.TestApplication.TestUserEmail);
        await Assert.That(profileData.DisplayName).IsEqualTo(Infrastructure.TestApplication.TestUserDisplayName);
        await Assert.That(profileData.MustChangePassword).IsFalse();
        await Assert.That(profileData.UserId).IsNotEqualTo(Guid.Empty);
        await Assert.That(profileData.Roles).IsNotNull();
        await Assert.That(profileData.CreatedAt).IsNotEqualTo(default);
    }

    [Test]
    public async Task ProfileEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange - Create unauthenticated client
        var apiClient = CreateApiClient();

        // Act
        var result = await apiClient.Auth.GetProfileAsync();

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ProfileEndpoint_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange - Create client with invalid token
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token-string");

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ProfileEndpoint_WithMalformedToken_ShouldReturnUnauthorized()
    {
        // Arrange - Create client with malformed JWT (not enough segments)
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt.structure");

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ProfileEndpoint_WithDeletedUser_ShouldReturnServiceUnavailable()
    {
        // Arrange - Create a new user and generate token, then delete the user
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var jwtTokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var tempUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"deleted_{Guid.CreateVersion7():N}@example.com",
            Email = $"deleted_{Guid.CreateVersion7():N}@example.com",
            DisplayName = "Deleted User",
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(tempUser, "TempPassword123!");
        await Assert.That(createResult.Succeeded).IsTrue();

        var (token, _) = jwtTokenGenerator.GenerateAccessToken(tempUser);

        // Delete the user
        var deleteResult = await userManager.DeleteAsync(tempUser);
        await Assert.That(deleteResult.Succeeded).IsTrue();

        // Create client with token for deleted user
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert - Should return 503 because user is in JWT but not in database
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task ProfileEndpoint_WithUserRoles_ShouldReturnRolesInProfile()
    {
        // Arrange - Create a new user with roles
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var jwtTokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        // Ensure test roles exist
        const string adminRole = "Admin";
        const string moderatorRole = "Moderator";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole) { Id = Guid.CreateVersion7() });
        }

        if (!await roleManager.RoleExistsAsync(moderatorRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(moderatorRole) { Id = Guid.CreateVersion7() });
        }

        var roleUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"roleuser_{Guid.CreateVersion7():N}@example.com",
            Email = $"roleuser_{Guid.CreateVersion7():N}@example.com",
            DisplayName = "Role User",
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(roleUser, "RolePassword123!");
        await Assert.That(createResult.Succeeded).IsTrue();

        // Add roles to user
        await userManager.AddToRoleAsync(roleUser, adminRole);
        await userManager.AddToRoleAsync(roleUser, moderatorRole);

        var (token, _) = jwtTokenGenerator.GenerateAccessToken(roleUser);

        // Create client with token
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var profileData = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        await Assert.That(profileData).IsNotNull();
        await Assert.That(profileData!.Roles).IsNotNull();
        await Assert.That(profileData.Roles.Count).IsEqualTo(2);
        await Assert.That(profileData.Roles).Contains(adminRole);
        await Assert.That(profileData.Roles).Contains(moderatorRole);
    }

    [Test]
    public async Task ProfileEndpoint_WithMustChangePasswordFlag_ShouldReflectFlag()
    {
        // Arrange - Create a new user with MustChangePassword = true
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var jwtTokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var passwordChangeUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"mustchange_{Guid.CreateVersion7():N}@example.com",
            Email = $"mustchange_{Guid.CreateVersion7():N}@example.com",
            DisplayName = "Must Change User",
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(passwordChangeUser, "TempPassword123!");
        await Assert.That(createResult.Succeeded).IsTrue();

        var (token, _) = jwtTokenGenerator.GenerateAccessToken(passwordChangeUser);

        // Create client with token
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var profileData = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        await Assert.That(profileData).IsNotNull();
        await Assert.That(profileData!.MustChangePassword).IsTrue();
    }

    [Test]
    public async Task ProfileEndpoint_WithCreatedAtTimestamp_ShouldReturnValidTimestamp()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var beforeRequest = DateTimeOffset.UtcNow;

        // Act
        var response = await client.GetAsync("/api/v1/auth/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var profileData = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        await Assert.That(profileData).IsNotNull();
        await Assert.That(profileData!.CreatedAt).IsNotEqualTo(default);

        // CreatedAt should be before the request (user was created during test setup)
        await Assert.That(profileData.CreatedAt).IsLessThanOrEqualTo(beforeRequest);

        // CreatedAt should be reasonable (within last 24 hours for test session)
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        await Assert.That(profileData.CreatedAt).IsGreaterThan(yesterday);
    }
}
