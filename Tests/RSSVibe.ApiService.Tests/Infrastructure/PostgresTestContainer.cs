using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace RSSVibe.ApiService.Tests.Infrastructure;

public class PostgresTestContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("library/postgres:18")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

#pragma warning disable CA1816
    public async ValueTask DisposeAsync()
#pragma warning restore CA1816
    {
        await _container.StopAsync();
    }
}
