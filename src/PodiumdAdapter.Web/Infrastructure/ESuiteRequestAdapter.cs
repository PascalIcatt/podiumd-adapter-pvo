using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace PodiumdAdapter.Web.Infrastructure
{
    public class ESuiteRequestAdapter : HttpClientRequestAdapter
    {
        public ESuiteRequestAdapter(HttpClient client, IConfiguration conf) : base(new ESuiteAuthProvider(conf), httpClient: client)
        {
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

    public static class ESuiteHttpClientRequestAdapterExtensions
    {
        public static ESuiteRequestAdapter GetEsuiteRequestAdapter(this IServiceProvider services, string baseUrlConfigKey)
        {
            var adapter = services.GetRequiredService<ESuiteRequestAdapter>();
            var config = services.GetRequiredService<IConfiguration>();
            adapter.BaseUrl = config[baseUrlConfigKey];
            return adapter;
        }
    }
}
