using RSSVibe.ApiService.Endpoints.Auth;
using RSSVibe.ApiService.Endpoints.FeedAnalyses;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace RSSVibe.ApiService.Endpoints;
/// <summary>
/// Registers all API endpoints under the /api/v1 route group with shared configuration.
/// </summary>
public static class ApiGroup
{
    /// <summary>
    /// Maps all API endpoints organized hierarchically under /api/v1.
    /// FluentValidation auto-validation is configured at the service level in Program.cs
    /// and applies to all endpoints in this group.
    /// </summary>
    public static IEndpointRouteBuilder MapApiV1(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        // Register all feature groups
        group.MapAuthGroup();
        group.MapFeedAnalysesGroup();
        group.AddFluentValidationAutoValidation();

        // Future feature groups can be added here
        // group.MapFeedsGroup();
        // group.MapUsersGroup();

        return endpoints;
    }
}
