using System.Net.Http.Json;
using System.Text.Json;

namespace RSSVibe.Contracts.Internal;

internal static class HttpHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ApiResult<TData>> HandleResponseAsync<TData>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<TData>(JsonOptions, cancellationToken);
            return ApiResult.Success(data!, statusCode);
        }

        var (title, detail) = await ExtractProblemDetailsAsync(response, cancellationToken);
        return ApiResult.Failure<TData>(statusCode, title, detail);
    }

    public static async Task<ApiResultNoData> HandleResponseNoDataAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            return ApiResult.Success(statusCode);
        }

        var (title, detail) = await ExtractProblemDetailsAsync(response, cancellationToken);
        return ApiResult.Failure(statusCode, title, detail);
    }

    private static async Task<(string? title, string? detail)> ExtractProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == "application/problem+json")
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(
                    JsonOptions,
                    cancellationToken);

                return (problemDetails?.Title, problemDetails?.Detail);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.ReasonPhrase, content);
        }
        catch
        {
            return (response.ReasonPhrase, null);
        }
    }

    private sealed record ProblemDetails(string? Title, string? Detail);
}
