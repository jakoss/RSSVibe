using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;
using System.Security.Claims;
using DataEntities = RSSVibe.Data.Entities;
using DataModels = RSSVibe.Data.Models;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

public static class GetFeedAnalysisEndpoint
{
    public static RouteGroupBuilder MapGetFeedAnalysisEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{analysisId:guid}", HandleAsync)
            .WithName("GetFeedAnalysis")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Summary = "Get feed analysis details";
                operation.Description = "Retrieves the complete analysis payload including selectors, preflight checks, and warnings.";
                return Task.CompletedTask;
            })
            .RequireAuthorization()
            .Produces<FeedAnalysisDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<FeedAnalysisDetailResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound>>
        HandleAsync(
            Guid analysisId,
            ClaimsPrincipal user,
            IFeedAnalysisService feedAnalysisService,
            CancellationToken ct)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new GetFeedAnalysisCommand(analysisId, userId);
        var result = await feedAnalysisService.GetFeedAnalysisAsync(command, ct);

        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.NotFound => TypedResults.NotFound(),
                FeedAnalysisError.Unauthorized => TypedResults.Forbid(),
                _ => TypedResults.NotFound()
            };
        }

        var analysis = result.Analysis!;
        var response = new FeedAnalysisDetailResponse(
            AnalysisId: analysis.Id,
            TargetUrl: analysis.TargetUrl,
            NormalizedUrl: analysis.NormalizedUrl,
            Status: (FeedAnalysisStatus)analysis.AnalysisStatus,
            PreflightChecks: ConvertPreflightChecks(analysis.PreflightChecks),
            PreflightDetails: MapPreflightDetails(analysis.PreflightDetails),
            Selectors: MapSelectors(analysis.Selectors),
            Warnings: analysis.Warnings,
            AiModel: analysis.AiModel,
            ApprovedFeedId: analysis.ApprovedFeedId,
            CreatedAt: analysis.CreatedAt,
            UpdatedAt: analysis.UpdatedAt
        );

        return TypedResults.Ok(response);
    }

    private static string[] ConvertPreflightChecks(DataEntities.FeedPreflightChecks preflightChecks)
    {
        if (preflightChecks == DataEntities.FeedPreflightChecks.None)
        {
            return [];
        }

        var result = new List<string>();

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.RequiresJavascript))
        {
            result.Add("RequiresJavascript");
        }

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.RequiresAuthentication))
        {
            result.Add("RequiresAuthentication");
        }

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.Paywalled))
        {
            result.Add("Paywalled");
        }

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.InvalidMarkup))
        {
            result.Add("InvalidMarkup");
        }

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.RateLimited))
        {
            result.Add("RateLimited");
        }

        if (preflightChecks.HasFlag(DataEntities.FeedPreflightChecks.UnknownIssue))
        {
            result.Add("UnknownIssue");
        }

        return [.. result];
    }

    private static FeedPreflightDetails MapPreflightDetails(DataModels.FeedPreflightDetails details)
    {
        return new FeedPreflightDetails(
            RequiresJavascript: details.RequiresJavascript,
            RequiresAuthentication: details.RequiresAuthentication,
            IsPaywalled: details.IsPaywalled,
            HasInvalidMarkup: details.HasInvalidMarkup,
            IsRateLimited: details.IsRateLimited,
            ErrorMessage: details.ErrorMessage,
            AdditionalInfo: details.AdditionalInfo
        );
    }

    private static FeedSelectors? MapSelectors(DataModels.FeedSelectors? selectors)
    {
        if (selectors is null)
        {
            return null;
        }

        return new FeedSelectors(
            List: selectors.ItemContainer,
            Item: selectors.Title,
            Title: selectors.Title,
            Link: selectors.Link,
            Published: selectors.PublishedDate,
            Summary: selectors.Description
        );
    }
}
