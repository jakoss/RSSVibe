using Microsoft.AspNetCore.Mvc.Testing;
using RSSVibe.ApiService.Tests.Infrastructure;
using System.Net.Http.Headers;

namespace RSSVibe.ApiService.Tests;

public abstract class TestsBase
{
    [ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
    public required TestApplication WebApplicationFactory { get; init; }

    /// <summary>
    /// Creates an HTTP client authenticated with the test user's bearer token.
    /// </summary>
    /// <param name="factory">Optional custom factory. If null, uses the shared TestApplication factory.</param>
    /// <param name="customToken">Optional custom JWT token. If null, uses the test user's token from shared factory.</param>
    /// <returns>An authenticated HttpClient instance.</returns>
    protected HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program>? factory = null,
        string? customToken = null)
    {
        var factoryToUse = factory ?? WebApplicationFactory;
        var client = factoryToUse.CreateClient();

        // Use custom token or get from base factory (shared test user)
        var token = customToken ?? WebApplicationFactory.TestUserBearerToken;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an HTTP client authenticated with a custom bearer token.
    /// </summary>
    /// <param name="customToken">The JWT token to use for authentication.</param>
    /// <returns>An authenticated HttpClient instance.</returns>
    protected HttpClient CreateAuthenticatedClient(string customToken)
    {
        return CreateAuthenticatedClient(null, customToken);
    }
}
