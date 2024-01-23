using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public static class UrlRewriteExtensions
    {
        private static readonly ConcurrentDictionary<string, ReplacerList> s_cache = new();

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var replacerList = GetReplacers(context);

            if (replacerList != null)
            {
                context.WrapFeature<IHttpResponseBodyFeature>(x => new UrlRewriteResponseBodyFeature(x, replacerList));
                context.WrapFeature<IHttpRequestFeature>(x => new UrlRewriteRequestFeature(x, replacerList));
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

        private static ReplacerList? GetReplacers(HttpContext context)
        {
            if (context?.Request == null) return null;

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var proxyRootstring = config["ESUITE_BASE_URL"];
            if (proxyRootstring == null) return null;

            var clients = context.RequestServices.GetServices<IESuiteClientConfig>();

            return s_cache.GetOrAdd(context.Request.Host.Host, (host, tup) =>
            {
                var (clients, config, request) = tup;

                var requestUrl = new UriBuilder
                {
                    Host = host,
                    Port = request.Host.Port.GetValueOrDefault(),
                    Scheme = request.Scheme,
                };

                var proxyUrl = new UriBuilder(proxyRootstring);

                var localRootString = request.ToString()!;

                var replacers = new List<Replacer>();

                foreach (var item in clients)
                {
                    proxyUrl!.Path = item.ProxyBasePath;
                    requestUrl.Path = item.RootUrl;
                    replacers.Add(new(requestUrl.ToString(), proxyUrl.ToString()));
                }

                return new(localRootString, proxyRootstring, replacers);
            }, (clients, config, context.Request));
        }
    }
}
