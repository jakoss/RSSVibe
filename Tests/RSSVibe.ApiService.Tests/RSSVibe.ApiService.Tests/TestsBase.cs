using System.Net.Http.Headers;
using RSSVibe.ApiService.Tests.Infrastructure;

namespace RSSVibe.ApiService.Tests;

public abstract class TestsBase
{
    [ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
    public required TestApplication WebApplicationFactory { get; init; }

    /// <summary>
    /// Creates an HTTP client authenticated with the test user's bearer token.
    /// </summary>
    /// <returns>An authenticated HttpClient instance.</returns>
    protected HttpClient CreateAuthenticatedClient()
    {
        var client = WebApplicationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", WebApplicationFactory.TestUserBearerToken);
        return client;
    }
}
