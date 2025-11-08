using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Data;
using RSSVibe.Services.FeedAnalyses;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Test implementation of IPreflightService that doesn't make real HTTP calls.
/// </summary>
internal sealed class TestPreflightService : IPreflightService
{
    public Task<PreflightCheckResult> PerformPreflightChecksAsync(string targetUrl, CancellationToken cancellationToken = default)
    {
        // Return a successful preflight result for testing
        var result = new PreflightCheckResult(
            Data.Entities.FeedPreflightChecks.None,
            new Data.Models.FeedPreflightDetails
            {
                RequiresJavascript = false,
                RequiresAuthentication = false,
                IsPaywalled = false,
                HasInvalidMarkup = false,
                IsRateLimited = false,
                AdditionalInfo = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "statusCode", "200" },
                    { "contentType", "text/html" }
                })
            },
            [],
            false
        );
        return Task.FromResult(result);
    }
}

/// <summary>
/// Integration tests for CreateFeedAnalysisEndpoint (POST /api/v1/feed-analyses).
/// Uses test implementation of IPreflightService to avoid real HTTP calls.
/// </summary>
public class CreateFeedAnalysisEndpointTests : TestsBase
{
    /// <summary>
    /// Creates a custom factory with test preflight service.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWithTestPreflight()
    {
        return WebApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real preflight service with test implementation
                services.RemoveAll<IPreflightService>();
                services.AddSingleton<IPreflightService, TestPreflightService>();
            });
        });
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithValidRequest_ShouldReturnAcceptedWithAnalysisData()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = CreateAuthenticatedClient(factory);

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/blog",
            AiModel: "openrouter/gpt-4.1-mini",
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Accepted);

        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.AnalysisId).IsNotEqualTo(Guid.Empty);
        await Assert.That(responseData.Status).IsEqualTo("Pending");
        await Assert.That(responseData.NormalizedUrl).IsEqualTo("https://example.com/blog");
        await Assert.That(responseData.Preflight).IsNotNull();

        // Verify Location header
        await Assert.That(response.Headers.Location?.ToString())
            .IsEqualTo($"/api/v1/feed-analyses/{responseData.AnalysisId}");
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithDuplicateUrl_ShouldReturnConflict()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = CreateAuthenticatedClient(factory);

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/duplicate",
            AiModel: null,
            ForceReanalysis: false
        );

        // Create first analysis
        await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Act - Try to create duplicate
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithInvalidUrl_ShouldReturnValidationError()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = CreateAuthenticatedClient(factory);

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "not-a-valid-url",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithInternalUrl_ShouldReturnForbidden()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = CreateAuthenticatedClient(factory);

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "http://localhost:8080/admin",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = factory.CreateClient();

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/blog",
            AiModel: null,
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateFeedAnalysisEndpoint_VerifyDatabaseStateAfterCreation()
    {
        // Arrange
        var factory = CreateFactoryWithTestPreflight();
        var client = CreateAuthenticatedClient(factory);

        var request = new CreateFeedAnalysisRequest(
            TargetUrl: "https://example.com/db-test",
            AiModel: "test-model",
            ForceReanalysis: false
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/feed-analyses", request);
        var responseData = await response.Content.ReadFromJsonAsync<CreateFeedAnalysisResponse>();
        await Assert.That(responseData).IsNotNull();

        // Assert - Check database state
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var analysis = await dbContext.FeedAnalyses
            .AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.TargetUrl,
                e.NormalizedUrl,
                e.AnalysisStatus,
                e.AiModel,
                e.Selectors
            })
            .FirstOrDefaultAsync(a => a.Id == responseData.AnalysisId);

        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.TargetUrl).IsEqualTo("https://example.com/db-test");
        await Assert.That(analysis.NormalizedUrl).IsEqualTo("https://example.com/db-test");
        await Assert.That(analysis.AnalysisStatus).IsEqualTo(Data.Entities.FeedAnalysisStatus.Pending);
        await Assert.That(analysis.AiModel).IsEqualTo("test-model");
    }
}
