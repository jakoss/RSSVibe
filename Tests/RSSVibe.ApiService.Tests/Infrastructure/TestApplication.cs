using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using RSSVibe.Services.Auth;
using TUnit.Core.Interfaces;

namespace RSSVibe.ApiService.Tests.Infrastructure;

public class TestApplication : WebApplicationFactory<Program>, IAsyncInitializer
{
    [ClassDataSource<PostgresTestContainer>]
    public required PostgresTestContainer Postgres { get; init; }

    /// <summary>
    /// Gets the bearer token for the test user.
    /// </summary>
    public string TestUserBearerToken { get; private set; } = string.Empty;

    /// <summary>
    /// Test user credentials.
    /// </summary>
    public const string TestUserEmail = "test@rssvibe.local";
    public const string TestUserPassword = "TestPassword123!";
    public const string TestUserDisplayName = "Test User";

    public async Task InitializeAsync()
    {
        _ = Server;

        await using var scope = Server.Services.CreateAsyncScope();

        await ApplyDatabaseMigrationsAsync(scope);
        await CreateTestUserAsync(scope);
    }

    /// <summary>
    /// Applies pending database migrations.
    /// </summary>
    private static async Task ApplyDatabaseMigrationsAsync(AsyncServiceScope scope)
    {
        await using var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates the test user and generates authentication token.
    /// </summary>
    private async Task CreateTestUserAsync(AsyncServiceScope scope)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var jwtTokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var testUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = TestUserEmail,
            Email = TestUserEmail,
            DisplayName = TestUserDisplayName,
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(testUser, TestUserPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var (token, _) = jwtTokenGenerator.GenerateAccessToken(testUser);
        TestUserBearerToken = token;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set environment to IntegrationTests
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:rssvibedb", Postgres.ConnectionString },
                { "Jwt:SecretKey", "test-secret-key-for-integration-tests-minimum-32-characters" },
                { "Jwt:Issuer", "https://test.rssvibe.local" },
                { "Jwt:Audience", "rssvibe-test-client" },
                { "Jwt:AccessTokenExpirationMinutes", "60" },
                { "Jwt:RefreshTokenExpirationDays", "7" }
            });
        });

        return base.CreateHost(builder);
    }
}
