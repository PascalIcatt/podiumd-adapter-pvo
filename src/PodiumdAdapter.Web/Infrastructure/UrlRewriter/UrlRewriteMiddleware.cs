using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public static class UrlRewriteExtensions
    {
        private static readonly ConcurrentDictionary<string, UrlRewriterCollection> s_cache = new();

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var replacerList = GetReplacers(context);
            var responseBody = context.Features.Get<IHttpResponseBodyFeature>();
            var request = context.Features.Get<IHttpRequestFeature>();
            var requestPipe = context.Features.Get<IRequestBodyPipeFeature>();

            if (replacerList != null && responseBody != null && request != null && requestPipe != null)
            {
                var feature = new UrlRewriteFeature(request, requestPipe, responseBody, replacerList);
                context.Features.Set<IHttpResponseBodyFeature>(feature);
                context.Features.Set<IHttpRequestFeature>(feature);
                context.Features.Set<IRequestBodyPipeFeature>(feature);
            }

            return next(context);
        });

        private static bool WrapFeature<T>(this HttpContext httpContext, Func<T, T> wrap)
        {
            var inner = httpContext.Features.Get<T>();
            if (inner == null) return false;
            var wrapped = wrap(inner);
            httpContext.Features.Set(wrapped);

            return true;
        }

        private static UrlRewriterCollection? GetReplacers(HttpContext context)
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
                    Port = request.Host.Port.GetValueOrDefault(),
                    Scheme = request.Scheme,
                };

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
