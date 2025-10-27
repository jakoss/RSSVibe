using RSSVibe.ApiService.Tests.Infrastructure;

namespace RSSVibe.ApiService.Tests;

public abstract class TestsBase
{
    [ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
    public required TestApplication WebApplicationFactory { get; init; }
}
