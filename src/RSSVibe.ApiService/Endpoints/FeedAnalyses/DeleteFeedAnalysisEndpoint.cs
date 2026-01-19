using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Services.FeedAnalyses;
using System.Security.Claims;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class DeleteFeedAnalysisEndpoint
{
    public static RouteGroupBuilder MapDeleteFeedAnalysisEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{analysisId:guid}", HandleAsync)
            .WithName("DeleteFeedAnalysis")
            .WithSummary("Delete a feed analysis")
            .WithDescription("Cancels and deletes a pending or in-progress feed analysis. Completed, failed, and superseded analyses are preserved as historical records.")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid analysisId,
        ClaimsPrincipal user,
        IFeedAnalysisService feedAnalysisService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Problem(
                title: "Unauthorized",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var command = new DeleteFeedAnalysisCommand(analysisId, userId);
        var result = await feedAnalysisService.DeleteFeedAnalysisAsync(command, cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.NotFound => TypedResults.Problem(
                    title: "Analysis not found",
                    detail: "The requested feed analysis does not exist",
                    statusCode: StatusCodes.Status404NotFound),
                FeedAnalysisError.Unauthorized => TypedResults.Problem(
                    title: "Forbidden",
                    detail: "You don't have permission to delete this analysis",
                    statusCode: StatusCodes.Status403Forbidden),
                FeedAnalysisError.CannotCancelCompletedAnalysis => TypedResults.Problem(
                    title: "Cannot cancel completed analysis",
                    detail: result.ErrorDetail ?? "Only pending or in-progress analyses can be deleted",
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => TypedResults.Problem(
                    title: "Failed to delete analysis",
                    detail: result.ErrorDetail ?? "An error occurred while deleting the analysis",
                    statusCode: StatusCodes.Status503ServiceUnavailable)
            };
        }

        return TypedResults.NoContent();
    }
}
