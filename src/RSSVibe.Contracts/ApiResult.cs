namespace RSSVibe.Contracts;

/// <summary>
/// Represents the result of an API call with optional data.
/// </summary>
public sealed record ApiResult<TData>
{
    public bool IsSuccess { get; init; }
    public TData? Data { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorTitle { get; init; }
    public string? ErrorDetail { get; init; }
}

/// <summary>
/// Factory methods for creating ApiResult instances.
/// </summary>
public static class ApiResult
{
    public static ApiResult<TData> Success<TData>(TData data, int statusCode = 200)
        => new() { IsSuccess = true, Data = data, StatusCode = statusCode };

    public static ApiResult<TData> Failure<TData>(int statusCode, string? title = null, string? detail = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail };

    public static ApiResultNoData Success(int statusCode = 204)
        => new() { IsSuccess = true, StatusCode = statusCode };

    public static ApiResultNoData Failure(int statusCode, string? title = null, string? detail = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail };
}

/// <summary>
/// Represents the result of an API call without data (e.g., 204 No Content).
/// </summary>
public sealed record ApiResultNoData
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorTitle { get; init; }
    public string? ErrorDetail { get; init; }
}
