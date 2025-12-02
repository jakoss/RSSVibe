using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Services.Auth;
using RSSVibe.Services.FeedAnalyses;
using RSSVibe.Services.Feeds;

namespace RSSVibe.Services.Extensions;

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
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Register feed analysis services
        services.AddScoped<IFeedAnalysisService, FeedAnalysisService>();
        services.AddScoped<IPreflightService, PreflightService>();

        // Register feed services
        services.AddScoped<IFeedService, FeedService>();

        return services;
    }
}
