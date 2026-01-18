using RSSVibe.Contracts.FeedItems;
using RSSVibe.Contracts.Feeds;

namespace RSSVibe.Contracts;

/// <summary>
/// Type-safe client for feed endpoints.
/// </summary>
public interface IFeedsClient
{
    /// <summary>
    /// GET /api/v1/feeds - List feeds for current user.
    /// </summary>
    Task<ApiResult<ListFeedsResponse>> ListAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/feeds/{feedId} - Get feed details.
    /// </summary>
    Task<ApiResult<FeedDetailResponse>> GetAsync(
        Guid feedId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/feeds - Create a new feed.
    /// </summary>
    Task<ApiResult<CreateFeedResponse>> CreateAsync(
        CreateFeedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/feeds/{feedId} - Update feed settings.
    /// </summary>
    Task<ApiResultNoData> UpdateAsync(
        Guid feedId,
        UpdateFeedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/v1/feeds/{feedId} - Delete a feed.
    /// </summary>
    Task<ApiResultNoData> DeleteAsync(
        Guid feedId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/feeds/{feedId}/trigger-parse - Trigger immediate feed parse.
    /// </summary>
    Task<ApiResult<TriggerParseResponse>> TriggerParseAsync(
        Guid feedId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/feeds/{feedId}/items - List feed items for a specific feed.
    /// </summary>
    Task<ApiResult<ListFeedItemsResponse>> ListItemsAsync(
        Guid feedId,
        ListFeedItemsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/feeds/{feedId}/items/{itemId} - Get feed item details.
    /// </summary>
    Task<ApiResult<FeedItemDetailResponse>> GetItemAsync(
        Guid feedId,
        Guid itemId,
        CancellationToken cancellationToken = default);
}
