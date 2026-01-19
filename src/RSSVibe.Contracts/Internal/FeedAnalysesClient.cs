using RSSVibe.Contracts.FeedAnalyses;
using System.Net.Http.Json;

namespace RSSVibe.Contracts.Internal;

internal sealed class FeedAnalysesClient(HttpClient httpClient) : IFeedAnalysesClient
{
    private const string BaseRoute = "/api/v1/feed-analyses";

    public async Task<ApiResult<CreateFeedAnalysisResponse>> CreateAsync(
        CreateFeedAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            BaseRoute,
            request,
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<CreateFeedAnalysisResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<ListFeedAnalysesResponse>> ListAsync(
        ListFeedAnalysesRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = BuildQueryString(
            ("status", request.Status?.ToString()),
            ("sort", request.Sort),
            ("skip", request.Skip.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("take", request.Take.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("search", request.Search)
        );

        var response = await httpClient.GetAsync(
            $"{BaseRoute}{queryParams}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<ListFeedAnalysesResponse>(response, cancellationToken);
    }

    public async Task<ApiResult<FeedAnalysisDetailResponse>> GetAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BaseRoute}/{analysisId}",
            cancellationToken);

        return await HttpHelper.HandleResponseAsync<FeedAnalysisDetailResponse>(response, cancellationToken);
    }

    public async Task<ApiResultNoData> DeleteAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{BaseRoute}/{analysisId}",
            cancellationToken);

        return await HttpHelper.HandleResponseNoDataAsync(response, cancellationToken);
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
