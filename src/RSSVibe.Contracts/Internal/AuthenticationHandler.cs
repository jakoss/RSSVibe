using System.Net.Http.Headers;

namespace RSSVibe.Contracts.Internal;

/// <summary>
/// Delegating handler that adds Bearer authentication to outgoing HTTP requests.
/// </summary>
internal sealed class AuthenticationHandler(IAccessTokenProvider? tokenProvider = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (tokenProvider is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = await tokenProvider.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
