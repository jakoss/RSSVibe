using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.FeedAnalyses;
using RSSVibe.Services.FeedAnalyses;
using System.Security.Claims;
using DataEntities = RSSVibe.Data.Entities;

namespace RSSVibe.ApiService.Endpoints.FeedAnalyses;

/// <summary>
/// Endpoint for creating feed analysis requests.
/// </summary>
public static class CreateFeedAnalysisEndpoint
{
    /// <summary>
    /// Maps the POST /feed-analyses endpoint to the handler.
    /// </summary>
     public static RouteGroupBuilder MapCreateFeedAnalysisEndpoint(
         this RouteGroupBuilder group)
     {
         group.MapPost("/", HandleAsync)
             .WithName("CreateFeedAnalysis")
             .WithSummary("Create a new feed analysis")
             .WithDescription("""
                 Initiates AI-powered analysis and preflight checks for a submitted URL. 
                 Returns immediately with 202 Accepted and provides location header for polling.
                 """)
             .RequireAuthorization();

         return group;
     }

    /// <summary>
    /// Handles the feed analysis creation request.
    /// </summary>
    private static async Task<Results<
            Accepted<CreateFeedAnalysisResponse>,
            ValidationProblem,
            ProblemHttpResult,
            UnauthorizedHttpResult>>
        HandleAsync(
            CreateFeedAnalysisRequest request,
            ClaimsPrincipal user,
            IFeedAnalysisService feedAnalysisService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CreateFeedAnalysisEndpoint");

        // Extract UserId from JWT claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning("Invalid or missing user ID in JWT claims");
            return TypedResults.Unauthorized();
        }

        // Map request to service command
        var command = new CreateFeedAnalysisCommand(
            userId,
            request.TargetUrl,
            request.AiModel,
            request.ForceReanalysis);

        // Invoke service layer
        var result = await feedAnalysisService.CreateFeedAnalysisAsync(command, cancellationToken);

        // Handle service result
        if (!result.Success)
        {
            return result.Error switch
            {
                FeedAnalysisError.DuplicateAnalysis =>
                    TypedResults.Problem(
                        title: "Analysis already exists",
                        detail: "An analysis for this URL already exists. Use forceReanalysis to create a new one.",
                        statusCode: StatusCodes.Status409Conflict),

                FeedAnalysisError.ReanalysisCooldown =>
                    TypedResults.Problem(
                        title: "Too many requests",
                        detail: result.ErrorDetail ?? "Please wait 15 minutes between reanalysis attempts.",
                        statusCode: StatusCodes.Status429TooManyRequests),

                FeedAnalysisError.ForbiddenUrl =>
                    TypedResults.Problem(
                        title: "Forbidden URL",
                        detail: "Target URL points to a restricted network resource.",
                        statusCode: StatusCodes.Status403Forbidden),

                FeedAnalysisError.AiServiceUnavailable =>
                    TypedResults.Problem(
                        title: "AI service unavailable",
                        detail: "Unable to connect to AI provider. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                FeedAnalysisError.DatabaseError =>
                    TypedResults.Problem(
                        title: "Service unavailable",
                        detail: "Database temporarily unavailable. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),

                FeedAnalysisError.InvalidUrl =>
                    TypedResults.Problem(
                        title: "Invalid URL",
                        detail: result.ErrorDetail ?? "The provided URL could not be processed.",
                        statusCode: StatusCodes.Status400BadRequest),

                FeedAnalysisError.PreflightFailed =>
                    TypedResults.Problem(
                        title: "Preflight validation failed",
                        detail: result.ErrorDetail ?? "URL validation failed during preflight checks.",
                        statusCode: StatusCodes.Status422UnprocessableEntity),

                _ =>
                    TypedResults.Problem(
                        title: "Analysis creation failed",
                        detail: "An unexpected error occurred.",
                        statusCode: StatusCodes.Status400BadRequest)
            };
        }

        // Map preflight checks enum to string array for DTO
        var preflightChecks = ConvertPreflightChecksToStringArray(result.PreflightChecks!.Value);

        // Construct success response
        var response = new CreateFeedAnalysisResponse(
            result.AnalysisId,
            result.Status!,
            result.NormalizedUrl!,
            new FeedPreflightDto(preflightChecks, null),
            result.CreatedAt!.Value);

        logger.LogInformation(
            "Feed analysis created: {AnalysisId} for user {UserId}",
            result.AnalysisId, userId);

        // Return 202 Accepted with Location header
        var location = $"/api/v1/feed-analyses/{result.AnalysisId}";
        return TypedResults.Accepted(location, response);
    }

    /// <summary>
    /// Converts FeedPreflightChecks flags enum to string array for API response.
    /// </summary>
    private static string[] ConvertPreflightChecksToStringArray(DataEntities.FeedPreflightChecks checks)
    {
        if (checks == DataEntities.FeedPreflightChecks.None)
        {
            return [];
        }

        var result = new List<string>();

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.RequiresJavascript))
        {
            result.Add("RequiresJavascript");
        }

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.RequiresAuthentication))
        {
            result.Add("RequiresAuthentication");
        }

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.Paywalled))
        {
            result.Add("Paywalled");
        }

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.InvalidMarkup))
        {
            result.Add("InvalidMarkup");
        }

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.RateLimited))
        {
            result.Add("RateLimited");
        }

        if (checks.HasFlag(DataEntities.FeedPreflightChecks.UnknownIssue))
        {
            result.Add("UnknownIssue");
        }

        return [.. result];
    }
}
