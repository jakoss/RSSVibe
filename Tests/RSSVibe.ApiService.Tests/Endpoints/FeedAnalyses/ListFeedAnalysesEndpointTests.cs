using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using System.Net;
using System.Net.Http.Json;
using DataModels = RSSVibe.Data.Models;

namespace RSSVibe.ApiService.Tests.Endpoints.FeedAnalyses;

/// <summary>
/// Integration tests for the ListFeedAnalysesEndpoint (GET /api/v1/feed-analyses).
/// Uses real DbContext and authentication infrastructure provided by the test host.
/// </summary>
public class ListFeedAnalysesEndpointTests : TestsBase
{
    private const string _endpointUrl = "/api/v1/feed-analyses";

    [Test]
    public async Task ListFeedAnalyses_WithValidRequest_ShouldReturnPagedResults()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;

        dbContext.FeedAnalyses.Add(new FeedAnalysis
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TargetUrl = "https://test.example.com/feed",
            NormalizedUrl = "https://test.example.com/feed",
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
            AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?skip=0&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items.Length).IsGreaterThan(0);
        await Assert.That(data.Paging.TotalCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ListFeedAnalyses_WithStatusFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        var completedId = Guid.CreateVersion7();
        var pendingId = Guid.CreateVersion7();

        dbContext.FeedAnalyses.AddRange(
             new FeedAnalysis
             {
                 Id = completedId,
                 UserId = userId,
                 TargetUrl = "https://example.com/completed",
                 NormalizedUrl = "https://example.com/completed",
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
                 CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                 UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
             },
            new FeedAnalysis
            {
                Id = pendingId,
                UserId = userId,
                TargetUrl = "https://example.com/pending",
                NormalizedUrl = "https://example.com/pending",
                AnalysisStatus = Data.Entities.FeedAnalysisStatus.Pending,
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
            }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?status=Completed&skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items).IsNotEmpty();
        await Assert.That(data.Items.All(i => i.Status == "completed")).IsTrue();
    }

    [Test]
    public async Task ListFeedAnalyses_WithStatusFilter_Pending_ShouldReturnOnlyPendingAnalyses()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        dbContext.FeedAnalyses.AddRange(
            new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = $"https://pending-filter-test-{timestamp}.example.com/pending",
                NormalizedUrl = $"https://pending-filter-test-{timestamp}.example.com/pending",
                AnalysisStatus = Data.Entities.FeedAnalysisStatus.Pending,
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = $"https://pending-filter-test-{timestamp}.example.com/completed",
                NormalizedUrl = $"https://pending-filter-test-{timestamp}.example.com/completed",
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
            }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?status=Pending&skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items).IsNotEmpty();
        await Assert.That(data.Items.All(i => i.Status == "pending")).IsTrue();
    }

    [Test]
    public async Task ListFeedAnalyses_WithStatusFilter_InProgress_ShouldReturnOnlyInProgressAnalyses()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        dbContext.FeedAnalyses.AddRange(
            new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = $"https://inprogress-filter-test-{timestamp}.example.com/inprogress",
                NormalizedUrl = $"https://inprogress-filter-test-{timestamp}.example.com/inprogress",
                AnalysisStatus = Data.Entities.FeedAnalysisStatus.InProgress,
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
                AnalysisStartedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = $"https://inprogress-filter-test-{timestamp}.example.com/pending",
                NormalizedUrl = $"https://inprogress-filter-test-{timestamp}.example.com/pending",
                AnalysisStatus = Data.Entities.FeedAnalysisStatus.Pending,
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTimeOffset.UtcNow
            }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?status=InProgress&skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items).IsNotEmpty();
        await Assert.That(data.Items.All(i => i.Status == "inprogress")).IsTrue();
    }

    [Test]
    public async Task ListFeedAnalyses_WithSearchTerm_ShouldReturnMatchingResults()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        dbContext.FeedAnalyses.AddRange(
             new FeedAnalysis
             {
                 Id = Guid.CreateVersion7(),
                 UserId = userId,
                 TargetUrl = "https://search.example.com/match",
                 NormalizedUrl = "https://search.example.com/match",
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
                 CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                 UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
             },
             new FeedAnalysis
             {
                 Id = Guid.CreateVersion7(),
                 UserId = userId,
                 TargetUrl = "https://other.example.com/no-match",
                 NormalizedUrl = "https://other.example.com/no-match",
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
                 CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                 UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
             }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?search=search.example.com&skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items).IsNotEmpty();
        await Assert.That(data.Items.All(i => i.TargetUrl.Contains("search.example.com"))).IsTrue();
    }

    [Test]
    public async Task ListFeedAnalyses_WithSortParameter_ShouldReturnSortedResults()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        dbContext.FeedAnalyses.AddRange(
             new FeedAnalysis
             {
                 Id = Guid.CreateVersion7(),
                 UserId = userId,
                 TargetUrl = "https://example.com/old",
                 NormalizedUrl = "https://example.com/old",
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
                 CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(-10),
                 UpdatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(-10)
             },
            new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = "https://example.com/new",
                NormalizedUrl = "https://example.com/new",
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
                CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(-5),
                UpdatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(-5)
            }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?sort=createdAt:asc&skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items.Length).IsGreaterThanOrEqualTo(2);
        await Assert.That(data.Items.First().TargetUrl).IsEqualTo("https://example.com/old");
    }

    [Test]
    public async Task ListFeedAnalyses_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        var userId = WebApplicationFactory.TestUser.Id;

        for (var i = 0; i < 30; i++)
        {
            dbContext.FeedAnalyses.Add(new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TargetUrl = $"https://test.example.com/feed-{i}",
                NormalizedUrl = $"https://test.example.com/feed-{i}",
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
                AnalysisStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                AnalysisCompletedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        // Act - second page of size 10
        var response = await client.GetAsync(_endpointUrl + "?skip=10&take=10");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items.Length).IsEqualTo(10);
        await Assert.That(data.Paging.Skip).IsEqualTo(10);
        await Assert.That(data.Paging.Take).IsEqualTo(10);
        await Assert.That(data.Paging.TotalCount).IsGreaterThanOrEqualTo(30);
        await Assert.That(data.Paging.HasMore).IsTrue();
    }

    [Test]
    public async Task ListFeedAnalyses_WithInvalidSort_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?sort=invalid:sort&skip=0&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedAnalyses_WithNegativeSkip_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?skip=-1&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedAnalyses_WithTakeOutOfRange_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var responseLow = await client.GetAsync(_endpointUrl + "?skip=0&take=0");
        var responseHigh = await client.GetAsync(_endpointUrl + "?skip=0&take=51");

        // Assert
        await Assert.That(responseLow.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(responseHigh.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedAnalyses_WithEmptySearch_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?search=&skip=0&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedAnalyses_WithInvalidStatus_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?status=InvalidStatus&skip=0&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListFeedAnalyses_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = WebApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?skip=0&take=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ListFeedAnalyses_WithValidToken_ShouldReturnOnlyUserAnalyses()
    {
        // Arrange
        var client = CreateAuthenticatedClient();

        await using var scope = WebApplicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        var userId = WebApplicationFactory.TestUser.Id;
        var otherUserId = Guid.CreateVersion7();

        // Persist another user to satisfy FK constraint for otherUserId
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

        dbContext.FeedAnalyses.AddRange(
             new FeedAnalysis
             {
                 Id = Guid.CreateVersion7(),
                 UserId = userId,
                 TargetUrl = "https://example.com/user-owned",
                 NormalizedUrl = "https://example.com/user-owned",
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
             },
             new FeedAnalysis
             {
                 Id = Guid.CreateVersion7(),
                 UserId = otherUserId,
                 TargetUrl = "https://example.com/other-user",
                 NormalizedUrl = "https://example.com/other-user",
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
             }
        );

        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync(_endpointUrl + "?skip=0&take=20");
        var data = await response.Content.ReadFromJsonAsync<ListFeedAnalysesResponse>();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Items).IsNotEmpty();
        await Assert.That(data.Items.All(i => i.TargetUrl.Contains("user-owned") || !i.TargetUrl.Contains("other-user"))).IsTrue();
    }
}
