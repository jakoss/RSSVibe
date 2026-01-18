using Microsoft.AspNetCore.Http.HttpResults;
using RSSVibe.Contracts.Feeds;
using RSSVibe.Services.Feeds;
using System.Security.Claims;

namespace RSSVibe.ApiService.Endpoints.Feeds;

/// <summary>
/// Endpoint for listing feeds with pagination, filtering, and sorting.
/// GET /api/v1/feeds
/// </summary>
public static class ListFeedsEndpoint
{
    /// <summary>
    /// Maps the list feeds endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapListFeedsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", HandleAsync)
            .WithName("ListFeeds")
            .WithSummary("List feeds")
            .WithDescription("Retrieves a paginated list of feeds owned by the authenticated user with optional filtering and sorting.")
            .Produces<ListFeedsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    /// <summary>
    /// Handles the list feeds request.
    /// </summary>
    private static async Task<Results<Ok<ListFeedsResponse>, ValidationProblem, UnauthorizedHttpResult, UnprocessableEntity, ProblemHttpResult>>
        HandleAsync(
            [AsParameters] ListFeedsRequest request,
            ClaimsPrincipal user,
            IFeedService feedService,
            CancellationToken ct)
    {
        // Extract user ID from JWT claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        // Parse sort parameter
        var (sortField, sortDirection) = ParseSortParameter(request.Sort);

        // Parse NextParseBefore timestamp
        DateTimeOffset? nextParseBefore = null;
        if (!string.IsNullOrEmpty(request.NextParseBefore))
        {
            if (!DateTimeOffset.TryParse(request.NextParseBefore, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                return TypedResults.UnprocessableEntity();
            }
            nextParseBefore = parsed;
        }

        // Create service query
        var query = new ListFeedsQuery(
            UserId: userId,
            Skip: request.Skip,
            Take: request.Take,
            SortField: sortField,
            SortDirection: sortDirection,
            Status: request.Status,
            NextParseBefore: nextParseBefore,
            Search: request.Search,
            IncludeInactive: request.IncludeInactive
        );

        // Call service
        var result = await feedService.ListFeedsAsync(query, ct);

        // Handle service errors
        if (!result.Success)
        {
            return result.Error switch
            {
                FeedListError.DatabaseUnavailable => TypedResults.Problem(
                    title: "Service unavailable",
                    detail: "Unable to retrieve feeds at this time. Please try again later.",
                    statusCode: 503),
                _ => TypedResults.Problem(
                    title: "Service unavailable",
                    detail: "Unable to retrieve feeds at this time. Please try again later.",
                    statusCode: 503)
            };
        }

        // Map to response
        var items = result.Items?.Select(MapToDto).ToArray() ?? [];
        var paging = new Contracts.PagingDto(
            Skip: request.Skip,
            Take: request.Take,
            TotalCount: result.TotalCount,
            HasMore: request.Skip + request.Take < result.TotalCount
        );

        var response = new ListFeedsResponse(Items: items, Paging: paging);

        return TypedResults.Ok(response);
    }

    private static (string SortField, string SortDirection) ParseSortParameter(string sort)
    {
        var parts = sort.Split(':');
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }
        return ("lastParsedAt", "desc"); // Default fallback
    }

    private static FeedListItemDto MapToDto(FeedListItem item)
    {
        var updateIntervalUnit = item.UpdateIntervalUnit switch
        {
            "Hour" => UpdateIntervalUnit.Hour,
            "Day" => UpdateIntervalUnit.Day,
            "Week" => UpdateIntervalUnit.Week,
            _ => UpdateIntervalUnit.Hour // Default fallback
        };

        var updateInterval = new UpdateIntervalDto(
            Unit: updateIntervalUnit,
            Value: item.UpdateIntervalValue
        );

        return new FeedListItemDto(
            FeedId: item.FeedId,
            UserId: item.UserId,
            SourceUrl: item.SourceUrl,
            NormalizedSourceUrl: item.NormalizedSourceUrl,
            Title: item.Title,
            Description: item.Description,
            Language: item.Language,
            UpdateInterval: updateInterval,
            TtlMinutes: item.TtlMinutes,
            Etag: item.Etag,
            LastModified: item.LastModified,
            LastParsedAt: item.LastParsedAt,
            NextParseAfter: item.NextParseAfter,
            LastParseStatus: item.LastParseStatus,
            PendingParseCount: item.PendingParseCount,
            AnalysisId: item.AnalysisId,
            CreatedAt: item.CreatedAt,
            UpdatedAt: item.UpdatedAt,
            RssUrl: $"/feed/{item.UserId}/{item.FeedId}"
        );
    }
}
