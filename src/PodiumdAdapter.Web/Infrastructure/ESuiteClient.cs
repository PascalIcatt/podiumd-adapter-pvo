using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class ESuiteClientExtensions
    {
        public static void AddESuiteClient<T>(this IServiceCollection services, T clientConfig) where T : IESuiteClientConfig
        {
            var clientName = typeof(T).Name;
            var trimmedRootUrl = clientConfig.RootUrl.Trim('/');
            var remotePath = $"/{clientConfig.ProxyBasePath.AsSpan().Trim('/')}/";

            services.TryAddSingleton<IProxyConfigProvider, SimpleProxyProvider>();
            services.TryAddSingleton<IProxyConfig, SimpleProxyConfig>();

            services.AddSingleton<IESuiteClientConfig>(clientConfig);

            services.AddSingleton(s =>
            {
                var config = s.GetRequiredService<IConfiguration>();

                var token = config["ESUITE_TOKEN"];

                return new RouteConfig
                {
                    RouteId = clientName,
                    ClusterId = clientName,
                    Match = new RouteMatch { Path = $"/{trimmedRootUrl}/{{*any}}" },
                    Transforms = new Dictionary<string, string>[]
                    {
                        new()
                        {
                            ["PathRemovePrefix"] = $"/{trimmedRootUrl}"
                        },
                        new()
                        {
                            ["ResponseHeaderRemove"] = "Content-Length"
                        },
                        new()
                        {
                            ["X-Forwarded"] = "Remove"
                        },
                        new()
                        {
                            ["RequestHeader"] = "Authorization",
                            ["Set"] = "Bearer " + token
                        }
                    }
                };
            });

            services.AddSingleton(s =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseUrl = config["ESUITE_BASE_URL"];
                var baseUri = baseUrl == null ? null : new UriBuilder(baseUrl) { Path = remotePath }.ToString();

                return new ClusterConfig
                {
                    ClusterId = clientName,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        [clientName] = new DestinationConfig
                        {
                            Address = baseUri
                        }
                    },
                };
            });

            services.AddHttpClient(clientName, (s, x) =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseUrl = new UriBuilder(config["ESUITE_BASE_URL"]) { Path = remotePath };
                x.BaseAddress = baseUrl.Uri;
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["ESUITE_TOKEN"] ?? throw new Exception("No token found for key ESUITE_TOKEN"));
            });
        }

        public static void MapEsuiteEndpoints(this IEndpointRouteBuilder builder)
        {
            foreach (var item in builder.ServiceProvider.GetServices<IESuiteClientConfig>())
            {
                var clientName = item.GetType().Name;
                var getClient = () => builder.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
                var root = builder.MapGroup(item.RootUrl);
                item.MapCustomEndpoints(root, getClient);
            }
        }

        private class SimpleProxyProvider(IProxyConfig proxyConfig) : IProxyConfigProvider
        {
            public IProxyConfig GetConfig() => proxyConfig;
        }

        private class SimpleProxyConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new();

            public SimpleProxyConfig(IEnumerable<RouteConfig> routes, IEnumerable<ClusterConfig> clusters)
            {
                Routes = routes?.ToList() ?? throw new ArgumentNullException(nameof(routes));
                Clusters = clusters?.ToList() ?? throw new ArgumentNullException(nameof(clusters));
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<RouteConfig> Routes { get; }

            public IReadOnlyList<ClusterConfig> Clusters { get; }

            public IChangeToken ChangeToken { get; }
        }
    }

    public interface IESuiteClientConfig
    {
        string ProxyBasePath { get; }
        string RootUrl { get; }
        void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient);
    }
}
