using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Net;
using DataModels = RSSVibe.Data.Models;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Integration tests for the DeleteFeedAnalysisEndpoint (DELETE /api/v1/feed-analyses/{analysisId}).
/// Tests use real DbContext and authentication infrastructure provided by the test host.
/// All tests use the typed API client (IRSSVibeApiClient) rather than raw HttpClient.
/// </summary>
public class DeleteFeedAnalysisEndpointTests : TestsBase
{
    [Test]
    public async Task DeleteFeedAnalysis_WithValidPendingAnalysis_ShouldReturn204NoContent()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var uniqueUrl = $"https://example.com/feed-{analysisId}";

        // Create a pending analysis
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = uniqueUrl,
            NormalizedUrl = uniqueUrl,
            AnalysisStatus = FeedAnalysisStatus.Pending,
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
            Warnings = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act - Use typed API client, NOT raw HttpClient
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        // Verify analysis was actually deleted
        await using var verifyScope = WebApplicationFactory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var deletedAnalysis = await verifyContext.FeedAnalyses.FindAsync(analysisId);
        await Assert.That(deletedAnalysis).IsNull();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithValidInProgressAnalysis_ShouldReturn204NoContent()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var uniqueUrl = $"https://example.com/feed-in-progress-{analysisId}";

        // Create an in-progress analysis
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = uniqueUrl,
            NormalizedUrl = uniqueUrl,
            AnalysisStatus = FeedAnalysisStatus.InProgress,
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
            Warnings = [],
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        // Verify analysis was actually deleted
        await using var verifyScope = WebApplicationFactory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var deletedAnalysis = await verifyContext.FeedAnalyses.FindAsync(analysisId);
        await Assert.That(deletedAnalysis).IsNull();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithCompletedAnalysis_ShouldReturn422UnprocessableEntity()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var uniqueUrl = $"https://example.com/feed-completed-{analysisId}";

        // Create a completed analysis
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = uniqueUrl,
            NormalizedUrl = uniqueUrl,
            AnalysisStatus = FeedAnalysisStatus.Completed,
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
            Warnings = [],
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorTitle).IsEqualTo("Cannot cancel completed analysis");

        // Verify analysis still exists
        await using var verifyScope = WebApplicationFactory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var existingAnalysis = await verifyContext.FeedAnalyses.FindAsync(analysisId);
        await Assert.That(existingAnalysis).IsNotNull();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithFailedAnalysis_ShouldReturn422UnprocessableEntity()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var uniqueUrl = $"https://example.com/feed-failed-{analysisId}";

        // Create a failed analysis
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = uniqueUrl,
            NormalizedUrl = uniqueUrl,
            AnalysisStatus = FeedAnalysisStatus.Failed,
            PreflightDetails = new DataModels.FeedPreflightDetails
            {
                RequiresJavascript = false,
                RequiresAuthentication = false,
                IsPaywalled = false,
                HasInvalidMarkup = false,
                IsRateLimited = false,
                ErrorMessage = "Test failure",
                AdditionalInfo = "{}"
            },
            Selectors = new DataModels.FeedSelectors(),
            Warnings = [],
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithSupersededAnalysis_ShouldReturn422UnprocessableEntity()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var uniqueUrl = $"https://example.com/feed-superseded-{analysisId}";

        // Create a superseded analysis
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = uniqueUrl,
            NormalizedUrl = uniqueUrl,
            AnalysisStatus = FeedAnalysisStatus.Superseded,
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
            Warnings = [],
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithNonExistentAnalysis_ShouldReturn404NotFound()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();
        var nonExistentId = Guid.CreateVersion7();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(nonExistentId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorTitle).IsEqualTo("Analysis not found");
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithOtherUsersAnalysis_ShouldReturn403Forbidden()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

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
        var otherUserUrl = $"https://example.com/other-user-feed-{analysisId}";
        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = analysisId,
            UserId = otherUserId,
            TargetUrl = otherUserUrl,
            NormalizedUrl = otherUserUrl,
            AnalysisStatus = FeedAnalysisStatus.Pending,
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
            Warnings = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act - Try to delete other user's analysis
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert - Should return 403 Forbidden to avoid leaking information
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorTitle).IsEqualTo("Forbidden");

        // Verify analysis still exists
        await using var verifyScope = WebApplicationFactory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var existingAnalysis = await verifyContext.FeedAnalyses.FindAsync(analysisId);
        await Assert.That(existingAnalysis).IsNotNull();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Arrange
        var apiClient = CreateApiClient(); // Unauthenticated client
        var analysisId = Guid.CreateVersion7();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithLinkedFeed_ShouldSetFeedAnalysisIdToNull()
    {
        // Arrange
        var apiClient = CreateAuthenticatedApiClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var analysisId = Guid.CreateVersion7();
        var feedId = Guid.CreateVersion7();

        // Create a pending analysis
        var linkedFeedUrl = $"https://example.com/linked-feed-{analysisId}";
        var analysis = new FeedAnalysis
        {
            Id = analysisId,
            UserId = userId,
            TargetUrl = linkedFeedUrl,
            NormalizedUrl = linkedFeedUrl,
            AnalysisStatus = FeedAnalysisStatus.Pending,
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
            Warnings = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.FeedAnalyses.Add(analysis);

        // Create a feed linked to this analysis
        dbContext.Feeds.Add(new Feed
        {
            Id = feedId,
            UserId = userId,
            SourceUrl = "https://example.com/feed.xml",
            NormalizedSourceUrl = "https://example.com/feed.xml",
            Title = "Test Feed",
            AnalysisId = analysisId, // Link to the analysis
            Selectors = new DataModels.FeedSelectors(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await apiClient.FeedAnalyses.DeleteAsync(analysisId, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        // Verify feed's AnalysisId was set to null due to cascade behavior
        await using var verifyScope = WebApplicationFactory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var feed = await verifyContext.Feeds.FindAsync(feedId);
        await Assert.That(feed).IsNotNull();
        await Assert.That(feed!.AnalysisId).IsNull();

        // Verify analysis was deleted
        var deletedAnalysis = await verifyContext.FeedAnalyses.FindAsync(analysisId);
        await Assert.That(deletedAnalysis).IsNull();
    }

    [Test]
    public async Task DeleteFeedAnalysis_WithInvalidGuidFormat_ShouldReturn404NotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act - Attempt with invalid GUID format (routing won't match guid constraint)
        var response = await client.DeleteAsync("/api/v1/feed-analyses/invalid-guid-format");

        // Assert - ASP.NET routing won't match this pattern, so it returns 404
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
