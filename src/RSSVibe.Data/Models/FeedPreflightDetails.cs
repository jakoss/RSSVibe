namespace RSSVibe.Data.Models;

public sealed class FeedPreflightDetails
{
    public bool RequiresJavascript { get; init; }
    public bool RequiresAuthentication { get; init; }
    public bool IsPaywalled { get; init; }
    public bool HasInvalidMarkup { get; init; }
    public bool IsRateLimited { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string>? AdditionalInfo { get; init; }
}
