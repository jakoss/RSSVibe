namespace RSSVibe.ApiService.Endpoints.Feeds;

/// <summary>
/// Route group for feed-related endpoints.
/// /api/v1/feeds
/// </summary>
public static class FeedsGroup
{
    /// <summary>
    /// Maps all feed endpoints to the route group.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedsGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/feeds")
            .WithTags("Feeds")
            .RequireAuthorization(); // All feed endpoints require authentication

        // Register endpoints
        group.MapListFeedsEndpoint();

        return endpoints;
    }
}
