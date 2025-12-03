namespace RSSVibe.Contracts.Internal;

/// <summary>
/// Internal implementation of the RSSVibe API client.
/// </summary>
internal sealed class RSSVibeApiClient(HttpClient httpClient) : IRSSVibeApiClient
{
    public IAuthClient Auth => field ??= new AuthClient(httpClient);
    public IFeedAnalysesClient FeedAnalyses => field ??= new FeedAnalysesClient(httpClient);
    public IFeedsClient Feeds => field ??= new FeedsClient(httpClient);
}
