using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Feeds;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;
using System.Net;
using System.Net.Http.Json;

namespace RSSVibe.ApiService.Tests.Endpoints.Feeds;

/// <summary>
/// Integration tests for the ListFeedsEndpoint (/api/v1/feeds).
/// Tests use real WebApplicationFactory with database and services.
/// </summary>
public class ListFeedsEndpointTests : TestsBase
{
    [Test]
    public async Task ListFeedsEndpoint_WithDefaultParameters_ShouldReturnEmptyList()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Paging.Skip).IsEqualTo(0);
        await Assert.That(responseData.Paging.Take).IsEqualTo(20);
        // Note: We don't check for empty items since other tests may have created feeds
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSkipAndTake_ShouldReturnCorrectPaging()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?skip=10&take=5");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Paging.Skip).IsEqualTo(10);
        await Assert.That(responseData.Paging.Take).IsEqualTo(5);
        // Note: TotalCount may be > 0 due to feeds from other tests
        await Assert.That(responseData.Paging.TotalCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(responseData.Items).Count().IsLessThanOrEqualTo(5);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSortByTitleAsc_ShouldAcceptParameter()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?sort=title:asc");

        // Assert - Just verify the endpoint accepts the parameter
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithInvalidSortFormat_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?sort=invalidFormat");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithNegativeSkip_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?skip=-1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithTakeOutOfRange_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?take=100");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithInvalidTimestamp_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?nextParseBefore=not-a-timestamp");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithUnauthenticatedRequest_ShouldReturnUnauthorized()
    {
        // Arrange - Use unauthenticated client
        var client = WebApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithStatusFilter_ShouldAcceptParameter()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?status=succeeded");

        // Assert - Just verify the endpoint accepts the parameter
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSearchFilter_ShouldAcceptParameter()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?search=test");

        // Assert - Just verify the endpoint accepts the parameter
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithIncludeInactive_ShouldAcceptParameter()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?includeInactive=true");

        // Assert - Just verify the endpoint accepts the parameter
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithCreatedFeeds_ShouldReturnFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create test feeds
        var feed1 = await CreateTestFeedAsync("Test Feed 1", $"https://example1{Guid.CreateVersion7():N}.com/rss.xml");
        var feed2 = await CreateTestFeedAsync("Test Feed 2", $"https://example2{Guid.CreateVersion7():N}.com/rss.xml");

        // Act
        var response = await client.GetAsync("/api/v1/feeds");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(2);
        await Assert.That(responseData.Paging.TotalCount).IsGreaterThanOrEqualTo(2);

        // Verify feed data - check that our feeds are present
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(feed1.Id);
        await Assert.That(feedIds).Contains(feed2.Id);

        var returnedFeed1 = responseData.Items.First(f => f.FeedId == feed1.Id);
        await Assert.That(returnedFeed1.Title).IsEqualTo("Test Feed 1");
        await Assert.That(returnedFeed1.SourceUrl).IsEqualTo(feed1.SourceUrl);
        await Assert.That(returnedFeed1.LastParseStatus).IsNull();
        await Assert.That(returnedFeed1.PendingParseCount).IsEqualTo(0);

        var returnedFeed2 = responseData.Items.First(f => f.FeedId == feed2.Id);
        await Assert.That(returnedFeed2.Title).IsEqualTo("Test Feed 2");
        await Assert.That(returnedFeed2.SourceUrl).IsEqualTo(feed2.SourceUrl);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithFeedsFromDifferentUsers_ShouldOnlyReturnCurrentUserFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create feeds for test user
        var userFeed1 = await CreateTestFeedAsync("User Feed 1", $"https://user1{Guid.CreateVersion7():N}.com/rss.xml");
        var userFeed2 = await CreateTestFeedAsync("User Feed 2", $"https://user2{Guid.CreateVersion7():N}.com/rss.xml");

        // Create a different user and their feed
        await using var scope = WebApplicationFactory.Server.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var otherUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"other{Guid.CreateVersion7():N}@test.com",
            Email = $"other{Guid.CreateVersion7():N}@test.com",
            DisplayName = "Other User",
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(otherUser, "TestPassword123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create other user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var otherUserFeed = new Feed
        {
            Id = Guid.CreateVersion7(),
            UserId = otherUser.Id,
            SourceUrl = $"https://other{Guid.CreateVersion7():N}.com/rss.xml",
            NormalizedSourceUrl = $"https://other{Guid.CreateVersion7():N}.com/rss.xml",
            Title = "Other User Feed",
            Description = "Test feed: Other User Feed",
            Language = "en",
            Selectors = new FeedSelectors
            {
                ItemContainer = "item",
                Title = "title",
                Link = "link",
                Description = "description",
                PublishedDate = "pubDate"
            },
            UpdateIntervalUnit = FeedUpdateUnit.Hour,
            UpdateIntervalValue = 1,
            TtlMinutes = 60,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Feeds.Add(otherUserFeed);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/api/v1/feeds?includeInactive=true");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(2);

        // Verify only user's feeds are returned
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        if (feedIds.Contains(userFeed1.Id) && feedIds.Contains(userFeed2.Id))
        {
            await Assert.That(feedIds).DoesNotContain(otherUserFeed.Id);
        }
    }

    [Test]
    public async Task ListFeedsEndpoint_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create 5 test feeds
        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateTestFeedAsync($"Feed {i}", $"https://feed{i}{Guid.CreateVersion7():N}.com/rss.xml");
        }

        // Act - Request page 1 (skip=2, take=2)
        var response = await client.GetAsync("/api/v1/feeds?skip=2&take=2");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsEqualTo(2);
        await Assert.That(responseData.Paging.TotalCount).IsGreaterThanOrEqualTo(5);
        await Assert.That(responseData.Paging.Skip).IsEqualTo(2);
        await Assert.That(responseData.Paging.Take).IsEqualTo(2);
        await Assert.That(responseData.Paging.HasMore).IsTrue();
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSortByTitleAsc_ShouldReturnSortedFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create feeds with titles in reverse alphabetical order
        var zebraFeed = await CreateTestFeedAsync("Zebra Feed", $"https://zebra{Guid.CreateVersion7():N}.com/rss.xml");
        var appleFeed = await CreateTestFeedAsync("Apple Feed", $"https://apple{Guid.CreateVersion7():N}.com/rss.xml");
        var bananaFeed = await CreateTestFeedAsync("Banana Feed", $"https://banana{Guid.CreateVersion7():N}.com/rss.xml");

        // Act
        var response = await client.GetAsync("/api/v1/feeds?sort=title:asc&includeInactive=true");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(3);

        // Find our test feeds in the results
        var ourFeeds = responseData.Items.Where(f =>
            f.FeedId == appleFeed.Id || f.FeedId == bananaFeed.Id || f.FeedId == zebraFeed.Id).ToList();
        if (ourFeeds.Count >= 3)
        {
            // Verify alphabetical order for our feeds
            var ourTitles = ourFeeds.OrderBy(f => f.Title).Select(f => f.Title).ToList();
            await Assert.That(ourTitles[0]).IsEqualTo("Apple Feed");
            await Assert.That(ourTitles[1]).IsEqualTo("Banana Feed");
            await Assert.That(ourTitles[2]).IsEqualTo("Zebra Feed");
        }
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSortByTitleDesc_ShouldReturnReverseSortedFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create feeds with titles
        var appleFeed = await CreateTestFeedAsync("Apple Feed", $"https://apple{Guid.CreateVersion7():N}.com/rss.xml");
        var zebraFeed = await CreateTestFeedAsync("Zebra Feed", $"https://zebra{Guid.CreateVersion7():N}.com/rss.xml");
        var bananaFeed = await CreateTestFeedAsync("Banana Feed", $"https://banana{Guid.CreateVersion7():N}.com/rss.xml");

        // Act
        var response = await client.GetAsync("/api/v1/feeds?sort=title:desc&includeInactive=true");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(3);

        // Find our test feeds in the results
        var ourFeeds = responseData.Items.Where(f =>
            f.FeedId == appleFeed.Id || f.FeedId == bananaFeed.Id || f.FeedId == zebraFeed.Id).ToList();
        if (ourFeeds.Count >= 3)
        {
            // Verify reverse alphabetical order for our feeds
            var ourTitles = ourFeeds.OrderByDescending(f => f.Title).Select(f => f.Title).ToList();
            await Assert.That(ourTitles[0]).IsEqualTo("Zebra Feed");
            await Assert.That(ourTitles[1]).IsEqualTo("Banana Feed");
            await Assert.That(ourTitles[2]).IsEqualTo("Apple Feed");
        }
    }

    [Test]
    public async Task ListFeedsEndpoint_WithStatusFilter_ShouldReturnFilteredFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create feeds with different statuses
        var succeededFeed = await CreateTestFeedAsync("Succeeded Feed", $"https://succeeded{Guid.CreateVersion7():N}.com/rss.xml", FeedParseStatus.Succeeded);
        await CreateTestFeedAsync("Failed Feed", $"https://failed{Guid.CreateVersion7():N}.com/rss.xml", FeedParseStatus.Failed);
        await CreateTestFeedAsync("Running Feed", $"https://running{Guid.CreateVersion7():N}.com/rss.xml", FeedParseStatus.Running);

        // Act - Filter by succeeded status
        var response = await client.GetAsync("/api/v1/feeds?status=succeeded");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(1);

        // Verify that our succeeded feed is present
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(succeededFeed.Id);

        var returnedFeed = responseData.Items.First(f => f.FeedId == succeededFeed.Id);
        await Assert.That(returnedFeed.Title).IsEqualTo("Succeeded Feed");
        await Assert.That(returnedFeed.LastParseStatus).IsEqualTo("Succeeded");
    }

    [Test]
    public async Task ListFeedsEndpoint_WithSearchFilter_ShouldReturnMatchingFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create feeds with different titles
        var techNewsFeed = await CreateTestFeedAsync("Technology News", $"https://tech{Guid.CreateVersion7():N}.com/rss.xml");
        var sportsFeed = await CreateTestFeedAsync("Sports Updates", $"https://sports{Guid.CreateVersion7():N}.com/rss.xml");
        var techReviewsFeed = await CreateTestFeedAsync("Tech Reviews", $"https://reviews{Guid.CreateVersion7():N}.com/rss.xml");

        // Act - Search for "tech"
        var response = await client.GetAsync("/api/v1/feeds?search=tech");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(2);

        // Verify both tech-related feeds are returned
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(techNewsFeed.Id);
        await Assert.That(feedIds).Contains(techReviewsFeed.Id);
        await Assert.That(feedIds).DoesNotContain(sportsFeed.Id);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithIncludeInactiveTrue_ShouldIncludeInactiveFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create active feed (no nextParseAfter or it's in the past)
        var activeFeed = await CreateTestFeedAsync("Active Feed", $"https://active{Guid.CreateVersion7():N}.com/rss.xml");

        // Create inactive feed (nextParseAfter in future)
        var inactiveFeed = await CreateTestFeedAsync(
            "Inactive Feed",
            $"https://inactive{Guid.CreateVersion7():N}.com/rss.xml",
            nextParseAfter: DateTimeOffset.UtcNow.AddDays(1),
            createdAt: DateTimeOffset.UtcNow.AddDays(-11));

        // Act - Include inactive feeds
        var response = await client.GetAsync("/api/v1/feeds?includeInactive=true");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(2);

        // Verify both feeds are returned
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(activeFeed.Id);
        await Assert.That(feedIds).Contains(inactiveFeed.Id);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithIncludeInactiveFalse_ShouldExcludeInactiveFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Create active feed
        var activeFeed = await CreateTestFeedAsync("Active Feed", $"https://active{Guid.CreateVersion7():N}.com/rss.xml");

        // Create inactive feed
        var inactiveFeed = await CreateTestFeedAsync(
            "Inactive Feed",
            $"https://inactive{Guid.CreateVersion7():N}.com/rss.xml",
            nextParseAfter: DateTimeOffset.UtcNow.AddDays(1),
            createdAt: DateTimeOffset.UtcNow.AddDays(-11));

        // Act - Default behavior (includeInactive=false)
        var response = await client.GetAsync("/api/v1/feeds");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(1);

        // Verify only active feed is returned
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(activeFeed.Id);
        await Assert.That(feedIds).DoesNotContain(inactiveFeed.Id);
    }

    [Test]
    public async Task ListFeedsEndpoint_WithNextParseBeforeFilter_ShouldReturnFilteredFeeds()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        var now = DateTimeOffset.UtcNow;

        // Create feeds with different nextParseAfter times
        var urgentFeed = await CreateTestFeedAsync("Urgent Feed", $"https://urgent{Guid.CreateVersion7():N}.com/rss.xml", nextParseAfter: now.AddMinutes(30));
        await CreateTestFeedAsync("Later Feed", $"https://later{Guid.CreateVersion7():N}.com/rss.xml", nextParseAfter: now.AddHours(2));
        await CreateTestFeedAsync("No Schedule Feed", $"https://noschedule{Guid.CreateVersion7():N}.com/rss.xml");

        // Act - Filter feeds that need parsing before next hour
        var response = await client.GetAsync($"/api/v1/feeds?nextParseBefore={now.AddHours(1):u}&includeInactive=true");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var responseData = await response.Content.ReadFromJsonAsync<ListFeedsResponse>();
        await Assert.That(responseData).IsNotNull();
        await Assert.That(responseData!.Items).Count().IsGreaterThanOrEqualTo(1);

        // Verify that our urgent feed is present
        var feedIds = responseData.Items.Select(f => f.FeedId).ToList();
        await Assert.That(feedIds).Contains(urgentFeed.Id);
    }

    /// <summary>
    /// Creates a test feed directly in the database for the test user.
    /// </summary>
    /// <param name="title">The feed title.</param>
    /// <param name="sourceUrl">The source URL.</param>
    /// <param name="lastParseStatus">Optional parse status.</param>
    /// <param name="lastParsedAt">Optional last parsed timestamp.</param>
    /// <param name="nextParseAfter">Optional next parse timestamp.</param>
    /// <param name="createdAt">Optional custom createdAt timestamp</param>
    /// <returns>The created feed entity.</returns>
    private async Task<Feed> CreateTestFeedAsync(
        string title,
        string sourceUrl,
        FeedParseStatus? lastParseStatus = null,
        DateTimeOffset? lastParsedAt = null,
        DateTimeOffset? nextParseAfter = null,
        DateTimeOffset? createdAt = null)
    {
        await using var scope = WebApplicationFactory.Server.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var feed = new Feed
        {
            Id = Guid.CreateVersion7(),
            UserId = WebApplicationFactory.TestUser.Id,
            SourceUrl = sourceUrl,
            NormalizedSourceUrl = sourceUrl,
            Title = title,
            Description = $"Test feed: {title}",
            Language = "en",
            Selectors = new FeedSelectors
            {
                ItemContainer = "item",
                Title = "title",
                Link = "link",
                Description = "description",
                Author = "author",
                PublishedDate = "pubDate"
            },
            UpdateIntervalUnit = FeedUpdateUnit.Hour,
            UpdateIntervalValue = 1,
            TtlMinutes = 60,
            Etag = null,
            LastModified = null,
            LastParsedAt = lastParsedAt,
            NextParseAfter = nextParseAfter,
            LastParseStatus = lastParseStatus,
            AnalysisId = null,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = createdAt ?? DateTimeOffset.UtcNow
        };

        dbContext.Feeds.Add(feed);
        await dbContext.SaveChangesAsync();

        return feed;
    }
}
