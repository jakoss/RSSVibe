namespace RSSVibe.Services.Feeds;

/// <summary>
/// Service for managing feeds.
/// </summary>
public interface IFeedService
{
    /// <summary>
    /// Lists feeds for a user with optional filtering and pagination.
    /// </summary>
    Task<ListFeedsResult> ListFeedsAsync(ListFeedsQuery query, CancellationToken ct);
}