namespace RSSVibe.Data.Models;

public sealed class HttpResponseHeaders
{
    public string? ContentType { get; init; }
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
    public string? CacheControl { get; init; }
    public string? Expires { get; init; }
    public int? ContentLength { get; init; }
    public string? Server { get; init; }
    public Dictionary<string, string>? CustomHeaders { get; init; }
}
