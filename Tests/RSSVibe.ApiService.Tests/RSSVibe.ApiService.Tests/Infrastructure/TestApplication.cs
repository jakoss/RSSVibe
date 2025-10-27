using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RSSVibe.Data;
using TUnit.Core.Interfaces;

namespace RSSVibe.ApiService.Tests.Infrastructure;

public class TestApplication : WebApplicationFactory<Program>, IAsyncInitializer
{
    [ClassDataSource<PostgresTestContainer>]
    public required PostgresTestContainer Postgres { get; init; }
    
    public async Task InitializeAsync()
    {
        _ = Server;

        await using var scope = Server.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:rssvibedb", Postgres.ConnectionString }
            });
        });
        
        return base.CreateHost(builder);
    }
}
