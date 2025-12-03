using Microsoft.EntityFrameworkCore;
using RSSVibe.Data;
using RSSVibe.Data.Entities;

namespace RSSVibe.Services.Feeds;

/// <summary>
/// Service for managing feeds.
/// </summary>
internal sealed class FeedService(RssVibeDbContext dbContext) : IFeedService
{
    /// <summary>
    /// Lists feeds for a user with optional filtering and pagination.
    /// </summary>
    public async Task<ListFeedsResult> ListFeedsAsync(ListFeedsQuery query, CancellationToken ct)
    {
        try
        {
            // Build base query with user filtering and read-only optimization
            var baseQuery = dbContext.Feeds
                .AsNoTracking()
                .Where(f => f.UserId == query.UserId);

            // Apply optional filters
            if (!string.IsNullOrEmpty(query.Status))
            {
                if (Enum.TryParse<FeedParseStatus>(query.Status, true, out var statusEnum))
                {
                    baseQuery = baseQuery.Where(f => f.LastParseStatus == statusEnum);
                }
                else
                {
                    // Invalid status, return no results
                    baseQuery = baseQuery.Where(f => false);
                }
            }

            if (query.NextParseBefore.HasValue)
            {
                baseQuery = baseQuery.Where(f => f.NextParseAfter.HasValue && f.NextParseAfter.Value < query.NextParseBefore.Value);
            }

            if (!string.IsNullOrEmpty(query.Search))
            {
                var searchTerm = $"%{query.Search.ToLowerInvariant()}%";
                baseQuery = baseQuery.Where(f =>
                    EF.Functions.ILike(f.Title, searchTerm) ||
                    EF.Functions.ILike(f.SourceUrl, searchTerm));
            }

            if (!query.IncludeInactive)
            {
                baseQuery = baseQuery.Where(f =>
                    f.LastParsedAt != null ||
                    f.CreatedAt > DateTime.UtcNow.AddDays(-7));
            }

            // Get total count for pagination
            var totalCount = await baseQuery.CountAsync(ct);

            // Apply sorting
            var orderedQuery = ApplySorting(baseQuery, query.SortField, query.SortDirection);

            // Apply pagination
            var paginatedQuery = orderedQuery
                .Skip(query.Skip)
                .Take(query.Take);

            // Execute query and map to results
            var feeds = await paginatedQuery
                .Select(f => new
                {
                    Feed = f,
                    PendingParseCount = f.ParseRuns.Count(pr => pr.Status == FeedParseStatus.Scheduled || pr.Status == FeedParseStatus.Running)
                })
                .ToListAsync(ct);

            var items = feeds.Select(x => MapToFeedListItem(x.Feed, x.PendingParseCount)).ToList();

            return new ListFeedsResult
            {
                Success = true,
                Items = items,
                TotalCount = totalCount,
                Error = null
            };
        }
        catch (Exception)
        {
            // In a real implementation, you'd log the exception
            return new ListFeedsResult
            {
                Success = false,
                Items = null,
                TotalCount = 0,
                Error = FeedListError.DatabaseUnavailable
            };
        }
    }

    private static IQueryable<Feed> ApplySorting(IQueryable<Feed> query, string sortField, string sortDirection)
    {
        return sortField.ToLowerInvariant() switch
        {
            "createdat" => sortDirection.Equals("desc" , StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(f => f.CreatedAt)
                : query.OrderBy(f => f.CreatedAt),
            "lastparsedat" => sortDirection.Equals("desc" , StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(f => f.LastParsedAt)
                : query.OrderBy(f => f.LastParsedAt),
            "title" => sortDirection.Equals("desc" , StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(f => f.Title)
                : query.OrderBy(f => f.Title),
            _ => query.OrderByDescending(f => f.LastParsedAt) // Default
        };
    }

    private static FeedListItem MapToFeedListItem(Feed feed, int pendingParseCount)
    {
        return new FeedListItem(
            FeedId: feed.Id,
            UserId: feed.UserId,
            SourceUrl: feed.SourceUrl,
            NormalizedSourceUrl: feed.NormalizedSourceUrl,
            Title: feed.Title,
            Description: feed.Description,
            Language: feed.Language,
            UpdateIntervalUnit: feed.UpdateIntervalUnit.ToString(),
            UpdateIntervalValue: feed.UpdateIntervalValue,
            TtlMinutes: feed.TtlMinutes,
            Etag: feed.Etag,
            LastModified: feed.LastModified?.UtcDateTime,
            LastParsedAt: feed.LastParsedAt?.UtcDateTime,
            NextParseAfter: feed.NextParseAfter?.UtcDateTime,
            LastParseStatus: feed.LastParseStatus?.ToString(),
            PendingParseCount: pendingParseCount,
            AnalysisId: feed.AnalysisId,
            CreatedAt: feed.CreatedAt.UtcDateTime,
            UpdatedAt: feed.UpdatedAt.UtcDateTime
        );
    }
}
