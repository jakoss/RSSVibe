using System.Net;
using System.Net.Http.Json;
using RSSVibe.Contracts.Auth;

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

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData.Email).IsEqualTo(request.Email);
        await Assert.That(responseData.DisplayName).IsEqualTo(request.DisplayName);
        await Assert.That(responseData.MustChangePassword).IsEqualTo(request.MustChangePassword);
        await Assert.That(responseData.UserId).IsNotEqualTo(Guid.Empty);

        // Verify Location header
        await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("/api/v1/auth/profile");
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

        // Assert
        await Assert.That(secondResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
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

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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

        var body = await response.Content.ReadAsStringAsync();
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
                Email: $"test_{Guid.NewGuid():N}@example.com",
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

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var responseData = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.MustChangePassword).IsTrue();
    }
}
