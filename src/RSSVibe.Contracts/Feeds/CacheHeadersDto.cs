namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// HTTP cache headers for feed delivery. Maps from HttpResponseHeaders entity model.
/// </summary>
public sealed record CacheHeadersDto(
    string? CacheControl,
    string? ETag,
    string? LastModified
);
