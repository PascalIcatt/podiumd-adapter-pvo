using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class ESuiteClientExtensions
    {
        public static void AddEsuiteClient<T>(this IServiceCollection services, T clientConfig) where T : IEsuiteClientConfig
        {
            var clientName = typeof(T).Name;
            var trimmed = clientConfig.RootUrl.Trim('/');

            services.TryAddSingleton<IProxyConfigProvider, SimpleProxyProvider>();
            services.TryAddSingleton<IProxyConfig, SimpleProxyConfig>();

            services.AddSingleton<IEsuiteClientConfig>(clientConfig);

            services.AddSingleton(s =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var token = config["ESUITE_TOKEN"];

                return new RouteConfig
                {
                    RouteId = clientName,
                    ClusterId = clientName,
                    Match = new RouteMatch { Path = $"/{trimmed}/{{*any}}" },
                    Transforms = new Dictionary<string, string>[]
                    {
                        new()
                        {
                            ["PathRemovePrefix"] = $"/{trimmed}"
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
                var baseUrl = config[clientConfig.ProxyBaseUrlConfigKey] ?? throw new Exception("No base uri found for key " + clientConfig.ProxyBaseUrlConfigKey);
                return new ClusterConfig
                {
                    ClusterId = clientName,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        [clientName] = new DestinationConfig
                        {
                            Address = baseUrl
                        }
                    },
                };
            });

            services.AddHttpClient(clientName, (s, x) =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseUrl = config[clientConfig.ProxyBaseUrlConfigKey] ?? throw new Exception("No base uri found for key " + clientConfig.ProxyBaseUrlConfigKey);
                x.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["ESUITE_TOKEN"] ?? throw new Exception("No token found for key ESUITE_TOKEN"));
            });
        }

        public static void MapEsuiteEndpoints(this IEndpointRouteBuilder builder)
        {
            foreach (var item in builder.ServiceProvider.GetServices<IEsuiteClientConfig>())
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

    public interface IEsuiteClientConfig
    {
        string ProxyBaseUrlConfigKey { get; }
        string RootUrl { get; }
        void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient);
    }
}
