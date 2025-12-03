using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;
using RSSVibe.Data.Entities;

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
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(201);

        var responseData = result.Data!;
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.Email).IsEqualTo(request.Email);
        await Assert.That(responseData.DisplayName).IsEqualTo(request.DisplayName);
        await Assert.That(responseData.MustChangePassword).IsEqualTo(request.MustChangePassword);
        await Assert.That(responseData.UserId).IsNotEqualTo(Guid.Empty);

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
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "duplicate@example.com",
            Password: "SecurePass123!",
            DisplayName: "First User",
            MustChangePassword: false
        );

        // First registration - should succeed
        var firstResult = await apiClient.Auth.RegisterAsync(request);
        await Assert.That(firstResult.IsSuccess).IsTrue();
        await Assert.That(firstResult.StatusCode).IsEqualTo(201);

        // Act - Second registration with same email
        var secondResult = await apiClient.Auth.RegisterAsync(request);

        // Assert - API result
        await Assert.That(secondResult.IsSuccess).IsFalse();
        await Assert.That(secondResult.StatusCode).IsEqualTo(409);

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
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "not-an-email",
            Password: "SecurePass123!",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);

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
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "weak",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task RegisterEndpoint_WithMissingPassword_ShouldReturnValidationError()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "",
            DisplayName: "Test User",
            MustChangePassword: false
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task RegisterEndpoint_WithMissingDisplayName_ShouldReturnValidationError()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "testuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "",
            MustChangePassword: false
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
    }

    [Test]
    public async Task RegisterEndpoint_WithPasswordRequirements_ShouldEnforceComplexity()
    {
        // Arrange
        var apiClient = CreateApiClient();

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
            var result = await apiClient.Auth.RegisterAsync(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(400);
        }
    }

    [Test]
    public async Task RegisterEndpoint_WithMustChangePasswordFlag_ShouldPreserveFlag()
    {
        // Arrange
        var apiClient = CreateApiClient();
        var request = new RegisterRequest(
            Email: "mustchange@example.com",
            Password: "SecurePass123!",
            DisplayName: "Must Change User",
            MustChangePassword: true
        );

        // Act
        var result = await apiClient.Auth.RegisterAsync(request);

        // Assert - API result
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.StatusCode).IsEqualTo(201);

        var responseData = result.Data!;
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
