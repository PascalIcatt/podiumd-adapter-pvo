using System.Collections.Concurrent;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class EsuiteUrlRewriteMaps
    {
        private static readonly ConcurrentDictionary<string, UrlRewriteMapCollection> s_cache = new();

        public static UrlRewriteMapCollection? GetRewriters(this HttpContext? context)
        {
            if (context?.Request == null) return null;

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var proxyRootstring = config["ESUITE_BASE_URL"];
            if (proxyRootstring == null) return null;

            var clients = context.RequestServices.GetServices<IESuiteClientConfig>();

            return s_cache.GetOrAdd(context.Request.Host.Host, (host, tup) =>
            {
                var (clients, config, request) = tup;

                var requestUriBuilder = new UriBuilder
                {
                    Host = host,
                    Scheme = request.Scheme,
                };

                if (request.Host.Port.HasValue)
                {
                    requestUriBuilder.Port = request.Host.Port.GetValueOrDefault();
                }

                var proxyUriBuilder = new UriBuilder(proxyRootstring);
                var proxyBaseUrl = proxyUriBuilder.Uri.ToString()!;
                var localBaseUrl = requestUriBuilder.Uri.ToString()!;

                var replacers = new List<UrlRewriteMap>();

                foreach (var item in clients)
                {
                    proxyUriBuilder.Path = item.ProxyBasePath;
                    requestUriBuilder.Path = item.RootUrl;
                    replacers.Add(new(requestUriBuilder.Uri.ToString(), proxyUriBuilder.Uri.ToString()));
                }

                return new(localBaseUrl, proxyBaseUrl, replacers);
            }, (clients, config, context.Request));
        }
    }
}
