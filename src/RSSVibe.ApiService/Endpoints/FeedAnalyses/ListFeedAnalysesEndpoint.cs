using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;
using System.Security.Claims;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class ListFeedAnalysesEndpoint
{
    public static RouteGroupBuilder MapListFeedAnalysesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("ListFeedAnalyses")
            .RequireAuthorization()
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Summary = "List feed analyses for current user";
                operation.Description = "Returns paginated list of feed analyses with filtering, sorting, and search capabilities.";
                return Task.CompletedTask;
            });

        return group;
    }

    private static async Task<Results<Ok<ListFeedAnalysesResponse>, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        ClaimsPrincipal user,
        IFeedAnalysisService feedAnalysisService,
        [AsParameters] ListFeedAnalysesRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var command = new ListFeedAnalysesCommand(
            userId,
            request.Status,
            request.Sort,
            request.Skip,
            request.Take,
            request.Search
        );

        var result = await feedAnalysisService.ListFeedAnalysesAsync(command, cancellationToken);

        if (!result.Success)
        {
            return TypedResults.Problem(
                title: "Failed to retrieve feed analyses",
                detail: result.Error?.ToString(),
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var response = new ListFeedAnalysesResponse(
            Items: [.. result.Items
                .Select(i => new FeedAnalysisListItemDto(
                    i.AnalysisId,
                    i.TargetUrl,
                    i.Status.ToString().ToLowerInvariant(),
                    i.Warnings,
                    i.AnalysisStartedAt,
                    i.AnalysisCompletedAt
                ))],
            Paging: new RSSVibe.Contracts.PagingDto(
                result.Paging.Skip,
                result.Paging.Take,
                result.Paging.TotalCount,
                result.Paging.HasMore
            )
        );

        return TypedResults.Ok(response);
    }
}
