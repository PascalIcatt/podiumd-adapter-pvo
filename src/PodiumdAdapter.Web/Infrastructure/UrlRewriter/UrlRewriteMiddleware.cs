using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public static class UrlRewriteExtensions
    {
        private static readonly ConcurrentDictionary<string, UrlRewriterCollection> s_cache = new();

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var rewriterCollection = GetRewriters(context);
            var responseBody = context.Features.Get<IHttpResponseBodyFeature>();
            var request = context.Features.Get<IHttpRequestFeature>();

            if (rewriterCollection != null && responseBody != null && request != null)
            {
                var feature = new UrlRewriteFeature(request, responseBody, rewriterCollection);
                context.Features.Set<IHttpResponseBodyFeature>(feature);
                context.Features.Set<IHttpRequestFeature>(feature);
                context.Features.Set<IRequestBodyPipeFeature>(feature);
            }

            return next(context);
        });

        private static UrlRewriterCollection? GetRewriters(HttpContext context)
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

                var replacers = new List<UrlRewriter>();

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
