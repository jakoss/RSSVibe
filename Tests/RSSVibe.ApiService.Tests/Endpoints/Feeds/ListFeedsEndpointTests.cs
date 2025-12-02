using System.Net;
using System.Net.Http.Json;
using RSSVibe.Contracts.Feeds;

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
        await Assert.That(responseData!.Items).IsEmpty();
        await Assert.That(responseData.Paging.TotalCount).IsEqualTo(0);
        await Assert.That(responseData.Paging.Skip).IsEqualTo(0);
        await Assert.That(responseData.Paging.Take).IsEqualTo(20);
        await Assert.That(responseData.Paging.HasMore).IsFalse();
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
        await Assert.That(responseData!.Items).IsEmpty();
        await Assert.That(responseData.Paging.TotalCount).IsEqualTo(0);
        await Assert.That(responseData.Paging.Skip).IsEqualTo(10);
        await Assert.That(responseData.Paging.Take).IsEqualTo(5);
        await Assert.That(responseData.Paging.HasMore).IsFalse();
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
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
}