using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace PodiumdAdapter.Web.Infrastructure
{
    public class ESuiteHttpClientRequestAdapter : HttpClientRequestAdapter
    {
        public ESuiteHttpClientRequestAdapter(HttpClient httpClient, IConfiguration conf) : base(new ESuiteAuthProvider(conf), httpClient: httpClient)
        {
            BaseUrl = conf["BaseUrl"];
        }

        private class ESuiteAuthProvider(IConfiguration conf) : BaseBearerTokenAuthenticationProvider(new ESuiteAccessTokenProvider(conf))
        {
        }

        private class ESuiteAccessTokenProvider(IConfiguration conf) : IAccessTokenProvider
        {
            public AllowedHostsValidator AllowedHostsValidator { get; } = new() { AllowedHosts = new[] { "*" } };

            public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(conf["Token"] ?? throw new Exception());
            }
        }
    }
}
