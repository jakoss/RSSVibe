namespace RSSVibe.Data.Entities;

[Flags]
public enum FeedPreflightChecks
{
    None = 0,
    RequiresJavascript = 1 << 0,
    RequiresAuthentication = 1 << 1,
    Paywalled = 1 << 2,
    InvalidMarkup = 1 << 3,
    RateLimited = 1 << 4,
    UnknownIssue = 1 << 5
}
