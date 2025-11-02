namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

/// <summary>
/// Registers all feed analysis-related endpoints under the /feed-analyses route group.
/// </summary>
public static class FeedAnalysesGroup
{
    /// <summary>
    /// Maps all feed analysis endpoints to the provided route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedAnalysesGroup(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/feed-analyses")
            .WithTags("Feed Analyses");

        // Register all endpoints in the feed analyses group
        group.MapCreateFeedAnalysisEndpoint();
        // TODO: Add other endpoints (GET list, GET detail, PATCH, etc.)

        return endpoints;
    }
}
