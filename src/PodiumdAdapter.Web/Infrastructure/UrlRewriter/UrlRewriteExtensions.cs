using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal.HttpClient;
using Yarp.ReverseProxy.Forwarder;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public delegate HttpMessageHandler WrapHandler(HttpMessageHandler inner);
    public delegate UrlRewriteMapCollection? GetUrlRewriteMapCollection();

    public static class UrlRewriteExtensions
    {
        public static void AddUrlRewriter(this IServiceCollection services)
        {
            services.TryAddSingleton<WrappingForwarderHttpClientFactory>();
            services.AddTransient<UrlRewriteHttpMessageHandler>();
            services.AddSingleton<IForwarderHttpClientFactory>(s => s.GetRequiredService<WrappingForwarderHttpClientFactory>());
            services.AddSingleton<WrapHandler>((s) => (h) => new UrlRewriteHttpMessageHandler(s.GetRequiredService<GetUrlRewriteMapCollection>()) { InnerHandler = h });
            services.ConfigureHttpClientDefaults(builder => builder.AddHttpMessageHandler<UrlRewriteHttpMessageHandler>());
        }

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var getMaps = context.RequestServices.GetRequiredService<GetUrlRewriteMapCollection>();
            var responseBody = context.Features.Get<IHttpResponseBodyFeature>();
            var maps = getMaps();
            if (responseBody != null && maps != null && maps.Count > 0)
            {
                var feature = new UrlRewriteFeature(context, responseBody, maps);
                context.Features.Set<IHttpResponseBodyFeature>(feature);
            }
            return next(context);
        });
    }
}
