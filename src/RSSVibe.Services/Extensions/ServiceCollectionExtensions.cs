namespace RSSVibe.Services.Extensions;

using RSSVibe.Services.Auth;

/// <summary>
/// Extension methods for registering RSSVibe services into the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all RSSVibe application services with the provided service collection.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddRssVibeServices(this IServiceCollection services)
    {
        // Register auth services
        services.AddScoped<IAuthService, AuthService>();

        // Future services will be added here
        // services.AddScoped<IFeedService, FeedService>();

        return services;
    }
}
