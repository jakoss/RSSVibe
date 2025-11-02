using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Net;
using System.Net.Http.Json;

namespace RSSVibe.ApiService.Tests.Endpoints.Auth;

/// <summary>
/// Integration tests for the RegisterEndpoint (/api/v1/auth/register).
/// Tests use real WebApplicationFactory with database and services.
/// </summary>
public class RegisterEndpointTests : TestsBase
{
    [Test]
    public async Task RegisterEndpoint_WithValidRequest_ShouldReturnCreatedWithUserData()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert - HTTP response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.Email).IsEqualTo(request.Email);
        await Assert.That(responseData.DisplayName).IsEqualTo(request.DisplayName);
        await Assert.That(responseData.MustChangePassword).IsEqualTo(request.MustChangePassword);
        await Assert.That(responseData.UserId).IsNotEqualTo(Guid.Empty);

        // Verify Location header
        await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("/api/v1/auth/profile");

        // Assert - Database state
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(request.Email);
        await Assert.That(user).IsNotNull();
        await Assert.That(user.Email).IsEqualTo(request.Email);
        await Assert.That(user.NormalizedEmail).IsEqualTo(request.Email.ToUpperInvariant());
        await Assert.That(user.DisplayName).IsEqualTo(request.DisplayName);
        await Assert.That(user.MustChangePassword).IsEqualTo(request.MustChangePassword);
        await Assert.That(user.Id).IsEqualTo(responseData.UserId);
    }

    [Test]
    public async Task RegisterEndpoint_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "duplicate@example.com",
            Password: "SecurePass123!",
            DisplayName: "First User",
            MustChangePassword: false
        );

        // First registration - should succeed
        var firstResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        // Act - Second registration with same email
        var secondResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert - HTTP response
        await Assert.That(secondResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);

        // Assert - Database state (only one user should exist)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var users = await dbContext.Users
            .Where(u => u.Email == request.Email)
            .ToListAsync();

        await Assert.That(users.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RegisterEndpoint_WithInvalidEmail_ShouldReturnValidationError()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "not-an-email",
            Password: "SecurePass123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert - HTTP response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        // Assert - Database state (no user should be created)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(request.Email);
        await Assert.That(user).IsNull();
    }

    [Test]
    public async Task RegisterEndpoint_WithWeakPassword_ShouldReturnValidationError()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "weak",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegisterEndpoint_WithMissingPassword_ShouldReturnValidationError()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegisterEndpoint_WithMissingDisplayName_ShouldReturnValidationError()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "",
            MustChangePassword: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        _ = await response.Content.ReadAsStringAsync();
        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegisterEndpoint_WithPasswordRequirements_ShouldEnforceComplexity()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Test cases for password validation requirements
        var testCases = new[]
        {
            ("nouppercase123!", "missing uppercase"),
            ("NOLOWERCASE123!", "missing lowercase"),
            ("NoNumbers!@#", "missing digits"),
            ("NoSpecialChar123", "missing special character"),
            ("Short1!", "too short (min 12 chars)")
        };

        foreach (var (password, _) in testCases)
        {
            var request = new RegisterRequest(
                Email: $"test_{Guid.CreateVersion7():N}@example.com",
                Password: password,
                DisplayName: "Test User",
                MustChangePassword: false
            );

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        }
    }

    [Test]
    public async Task RegisterEndpoint_WithMustChangePasswordFlag_ShouldPreserveFlag()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var request = new RegisterRequest(
            Email: "mustchange@example.com",
            Password: "SecurePass123!",
            DisplayName: "Must Change User",
            MustChangePassword: true
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert - HTTP response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.MustChangePassword).IsTrue();

        // Assert - Database state (flag should be persisted)
        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(request.Email);
        await Assert.That(user).IsNotNull();
        await Assert.That(user.MustChangePassword).IsTrue();
    }
}
