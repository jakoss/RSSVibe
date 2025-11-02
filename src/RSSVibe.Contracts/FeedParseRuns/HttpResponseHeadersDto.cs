namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// HTTP response headers from feed parse run. Maps from HttpResponseHeaders entity model stored in FeedParseRun.ResponseHeaders.
/// </summary>
public sealed record HttpResponseHeadersDto(
    string? ETag,
    string? LastModified,
    string? ContentType,
    string? CacheControl,
    string? Expires,
    int? ContentLength,
    string? Server
);
