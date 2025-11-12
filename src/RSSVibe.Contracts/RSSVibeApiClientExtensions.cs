using Microsoft.Extensions.DependencyInjection;
using RSSVibe.Contracts.Internal;

namespace RSSVibe.Contracts;

/// <summary>
/// Extension methods for registering the RSSVibe API client.
/// </summary>
public static class RSSVibeApiClientExtensions
{
    /// <summary>
    /// Adds the RSSVibe API client to the service collection.
    /// Configures a typed HttpClient with the specified base address.
    /// Automatically adds bearer token authentication if IAccessTokenProvider is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Optional configuration for the HttpClient.</param>
    /// <returns>An IHttpClientBuilder for further configuration.</returns>
    public static IHttpClientBuilder AddRSSVibeApiClient(
        this IServiceCollection services,
        Action<HttpClient>? configureClient = null)
    {
        return services.AddHttpClient<IRSSVibeApiClient, RSSVibeApiClient>(client =>
        {
            configureClient?.Invoke(client);
        })
        .AddHttpMessageHandler(sp =>
        {
            var tokenProvider = sp.GetService<IAccessTokenProvider>();
            return new AuthenticationHandler(tokenProvider);
        });
    }

    /// <summary>
    /// Adds the RSSVibe API client to the service collection with a base address.
    /// Automatically adds bearer token authentication if IAccessTokenProvider is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address for the API.</param>
    /// <returns>An IHttpClientBuilder for further configuration.</returns>
    public static IHttpClientBuilder AddRSSVibeApiClient(
        this IServiceCollection services,
        string baseAddress)
    {
        return services.AddRSSVibeApiClient(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        });
    }

    /// <summary>
    /// Adds the RSSVibe API client to the service collection with a base address.
    /// Automatically adds bearer token authentication if IAccessTokenProvider is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address for the API.</param>
    /// <returns>An IHttpClientBuilder for further configuration.</returns>
    public static IHttpClientBuilder AddRSSVibeApiClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        return services.AddRSSVibeApiClient(client =>
        {
            client.BaseAddress = baseAddress;
        });
    }
}
