namespace RSSVibe.Contracts.Internal;

/// <summary>
/// Internal implementation of the RSSVibe API client.
/// </summary>
internal sealed class RSSVibeApiClient(HttpClient httpClient) : IRSSVibeApiClient
{
    private IAuthClient? _auth;
    private IFeedAnalysesClient? _feedAnalyses;
    private IFeedsClient? _feeds;

    public IAuthClient Auth => _auth ??= new AuthClient(httpClient);
    public IFeedAnalysesClient FeedAnalyses => _feedAnalyses ??= new FeedAnalysesClient(httpClient);
    public IFeedsClient Feeds => _feeds ??= new FeedsClient(httpClient);
}
