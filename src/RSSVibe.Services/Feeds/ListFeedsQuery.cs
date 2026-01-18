namespace RSSVibe.Services.Feeds;

/// <summary>
/// Query to list feeds for a user.
/// </summary>
public sealed record ListFeedsQuery(
    Guid UserId,
    int Skip,
    int Take,
    string SortField,
    string SortDirection,
    string? Status,
    DateTimeOffset? NextParseBefore,
    string? Search,
    bool IncludeInactive
);
