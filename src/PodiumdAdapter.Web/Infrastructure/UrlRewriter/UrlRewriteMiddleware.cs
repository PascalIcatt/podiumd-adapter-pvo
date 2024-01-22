using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public static class UrlRewriteExtensions
    {
        private static readonly ConcurrentDictionary<string, IReadOnlyCollection<Replacer>> s_cache = new();

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var replacers = GetReplacers(context);

            if (replacers.Count != 0)
            {
                context.WrapFeature<IHttpResponseBodyFeature>(x => new UrlRewriteResponseBodyFeature(x, replacers));
                context.WrapFeature<IHttpRequestFeature>(x => new UrlRewriteRequestFeature(x, replacers));
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

        private static IReadOnlyCollection<Replacer> GetReplacers(HttpContext context)
        {
            if (context?.Request == null) return [];

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
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

                var replacers = new List<Replacer>();

                foreach (var item in clients)
                {
                    var targetUrl = config[item.ProxyBaseUrlConfigKey];
                    if (targetUrl == null) continue;
                    requestUrl.Path = item.RootUrl;
                    var sourceUrl = requestUrl.ToString();
                    var targetBytes = Encoding.UTF8.GetBytes(targetUrl);
                    var sourceBytes = Encoding.UTF8.GetBytes(sourceUrl);
                    replacers.Add(new(targetBytes, sourceBytes, targetUrl, sourceUrl));
                }

                return replacers;
            }, (clients, config, context.Request));
        }
    }
    public record Replacer(ReadOnlyMemory<byte> RemoteBytes, ReadOnlyMemory<byte> LocalBytes, string RemoteString, string LocalString);
}
