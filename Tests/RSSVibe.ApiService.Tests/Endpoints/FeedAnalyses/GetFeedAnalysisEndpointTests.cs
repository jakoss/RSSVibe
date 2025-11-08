using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Net;
using System.Net.Http.Json;
using DataModels = RSSVibe.Data.Models;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Integration tests for the GetFeedAnalysisEndpoint (GET /api/v1/feed-analyses/{analysisId}).
/// Tests use real DbContext and authentication infrastructure provided by the test host.
/// </summary>
public class GetFeedAnalysisEndpointTests : TestsBase
{
    [Test]
    public async Task GetFeedAnalysisEndpoint_WithValidAnalysisId_ShouldReturnAnalysisDetails()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();

        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = "https://example.com/feed",
            NormalizedUrl = "https://example.com/feed",
            AnalysisStatus = Data.Entities.FeedAnalysisStatus.Completed,
            PreflightDetails = new DataModels.FeedPreflightDetails
            {
                RequiresJavascript = false,
                RequiresAuthentication = false,
                IsPaywalled = false,
                HasInvalidMarkup = false,
                IsRateLimited = false,
                ErrorMessage = null,
                AdditionalInfo = "{}"
            },
            Selectors = new DataModels.FeedSelectors(),
            Warnings = ["Test warning"],
            AiModel = "test-model",
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysisId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync<FeedAnalysisDetailResponse>();
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.AnalysisId).IsEqualTo(analysisId);
        await Assert.That(data.TargetUrl).IsEqualTo("https://example.com/feed");
        await Assert.That(data.NormalizedUrl).IsEqualTo("https://example.com/feed");
        await Assert.That(data.Status).IsEqualTo(Contracts.FeedAnalyses.FeedAnalysisStatus.Completed);
        await Assert.That(data.AiModel).IsEqualTo("test-model");
        await Assert.That(data.Warnings.Length).IsEqualTo(1);
        await Assert.That(data.Warnings[0]).IsEqualTo("Test warning");
        await Assert.That(data.CreatedAt).IsGreaterThan(DateTimeOffset.MinValue);
        await Assert.That(data.UpdatedAt).IsGreaterThan(DateTimeOffset.MinValue);
    }

    [Test]
    public async Task GetFeedAnalysisEndpoint_WithNonExistentAnalysisId_ShouldReturnNotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var nonExistentId = Guid.CreateVersion7();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{nonExistentId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetFeedAnalysisEndpoint_WithOtherUsersAnalysisId_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var otherUserId = Guid.CreateVersion7();
        var analysisId = Guid.CreateVersion7();

        // Create another user
        dbContext.Users.Add(new ApplicationUser
        {
            Id = otherUserId,
            UserName = $"other_{otherUserId:N}@rssvibe.local",
            NormalizedUserName = $"OTHER_{otherUserId:N}@RSSVIBE.LOCAL",
            Email = $"other_{otherUserId:N}@rssvibe.local",
            NormalizedEmail = $"OTHER_{otherUserId:N}@RSSVIBE.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.CreateVersion7().ToString("N"),
            ConcurrencyStamp = Guid.CreateVersion7().ToString("N"),
            DisplayName = "Other Test User"
        });

        // Create analysis for other user
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = otherUserId,
            TargetUrl = "https://example.com/other-user-feed",
            NormalizedUrl = "https://example.com/other-user-feed",
            AnalysisStatus = Data.Entities.FeedAnalysisStatus.Completed,
            PreflightDetails = new DataModels.FeedPreflightDetails
            {
                RequiresJavascript = false,
                RequiresAuthentication = false,
                IsPaywalled = false,
                HasInvalidMarkup = false,
                IsRateLimited = false,
                ErrorMessage = null,
                AdditionalInfo = "{}"
            },
            Selectors = new DataModels.FeedSelectors(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act - Try to access other user's analysis
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysisId}");

        // Assert - Should return 403 Forbidden to avoid leaking information
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetFeedAnalysisEndpoint_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();
        var analysisId = Guid.CreateVersion7();

        // Act
        var response = await client.GetAsync($"/api/v1/feed-analyses/{analysisId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
