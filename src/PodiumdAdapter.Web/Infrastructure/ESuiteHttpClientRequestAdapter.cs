using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace PodiumdAdapter.Web.Infrastructure
{
    public class ESuiteHttpClientRequestAdapter : HttpClientRequestAdapter
    {
        public ESuiteHttpClientRequestAdapter(IHttpClientFactory factory, IConfiguration conf, string baseUrlConfigKey) : base(new ESuiteAuthProvider(conf), httpClient: factory.CreateClient())
        {
            BaseUrl = conf[baseUrlConfigKey];
        }

        private class ESuiteAuthProvider(IConfiguration conf) : BaseBearerTokenAuthenticationProvider(new ESuiteAccessTokenProvider(conf))
        {
        }

        private class ESuiteAccessTokenProvider(IConfiguration conf) : IAccessTokenProvider
        {
            public AllowedHostsValidator AllowedHostsValidator { get; } = new() { AllowedHosts = new[] { "*" } };

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(conf["ESUITE_TOKEN"] ?? throw new Exception());
        }
    }
}
}
