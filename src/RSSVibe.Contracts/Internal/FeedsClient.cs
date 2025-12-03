using RSSVibe.Contracts.Feeds;
using RSSVibe.Contracts.FeedItems;
using System.Net.Http.Json;

namespace RSSVibe.Contracts.Internal;

internal sealed class FeedsClient(HttpClient httpClient) : IFeedsClient
{
    private const string BaseRoute = "/api/v1/feeds";

    public async Task<ApiResult<ListFeedsResponse>> ListAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = BuildQueryString(
            ("skip", request.Skip.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("take", request.Take.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("sort", request.Sort),
            ("status", request.Status),
            ("search", request.Search)
        );

        var response = await httpClient.GetAsync(
            $"{BaseRoute}{queryParams}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<ListFeedsResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<FeedDetailResponse>> GetAsync(
        Guid feedId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BaseRoute}/{feedId}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<FeedDetailResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<CreateFeedResponse>> CreateAsync(
        CreateFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            BaseRoute,
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<CreateFeedResponse>(response, cancellationToken);
    }

    public async Task<ApiResultNoData> UpdateAsync(
        Guid feedId,
        UpdateFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{BaseRoute}/{feedId}",
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseNoDataAsync(response, cancellationToken);
    }

    public async Task<ApiResultNoData> DeleteAsync(
        Guid feedId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{BaseRoute}/{feedId}",
            cancellationToken);

        return await HttpHelper.HandleResponseNoDataAsync(response, cancellationToken);
    }

    public async Task<ApiResult<TriggerParseResponse>> TriggerParseAsync(
        Guid feedId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"{BaseRoute}/{feedId}/trigger-parse",
            null,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<TriggerParseResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<ListFeedItemsResponse>> ListItemsAsync(
        Guid feedId,
        ListFeedItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = BuildQueryString(
            ("skip", request.Skip.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("take", request.Take.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("sort", request.Sort),
            ("includeMetadata", request.IncludeMetadata.ToString(System.Globalization.CultureInfo.InvariantCulture))
        );

        var response = await httpClient.GetAsync(
            $"{BaseRoute}/{feedId}/items{queryParams}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<ListFeedItemsResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<FeedItemDetailResponse>> GetItemAsync(
        Guid feedId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BaseRoute}/{feedId}/items/{itemId}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<FeedItemDetailResponse>(response, cancellationToken);
    }

    private static string BuildQueryString(params (string key, string? value)[] parameters)
    {
        var validParams = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.value))
            .Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value!)}");

        var queryString = string.Join("&", validParams);
        return string.IsNullOrEmpty(queryString) ? string.Empty : $"?{queryString}";
    }
}
