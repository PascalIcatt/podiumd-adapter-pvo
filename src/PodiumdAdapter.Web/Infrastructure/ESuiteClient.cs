using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Yarp.ReverseProxy.Configuration;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class ESuiteClientExtensions
    {
        public static void AddEsuiteClient<T>(this IServiceCollection services, IEsuiteClientConfig<T> clientConfig) where T : class
        {
            var clientName = typeof(T).Name;
            var trimmed = clientConfig.RootUrl.Trim('/');

            services.TryAddSingleton<IProxyConfigProvider,SimpleProxyProvider>();
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
                    Transforms = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["PathRemovePrefix"] = $"/{trimmed}",
                        },
                        new Dictionary<string, string>
                        {
                            ["RequestHeader"] = "Authorization",
                            ["Set"] = "Bearer " + token
                        },
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

            services.AddSingleton(clientConfig);
            services.AddHttpClient(clientName, (s, x) =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseUrl = config[clientConfig.ProxyBaseUrlConfigKey] ?? throw new Exception("No base uri found for key " + clientConfig.ProxyBaseUrlConfigKey);
                x.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["ESUITE_TOKEN"] ?? throw new Exception("No token found for key ESUITE_TOKEN"));
            });
            services.AddTransient(s =>
            {
                var http = s.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
                var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http);
                return clientConfig.CreateClient(adapter);
            });
        }

        public static void MapEsuiteEndpoints(this IEndpointRouteBuilder builder)
        {
            foreach (var item in builder.ServiceProvider.GetServices<IEsuiteClientConfig>())
            {
                var root = builder.MapGroup(item.RootUrl);
                item.MapCustomEndpoints(root);
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
        void MapCustomEndpoints(IEndpointRouteBuilder clientRoot);
    }

    public interface IEsuiteClientConfig<T>: IEsuiteClientConfig where T : class
    {
        T CreateClient(IRequestAdapter requestAdapter);
    }
}
