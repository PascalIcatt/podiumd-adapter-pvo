using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

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

            services.AddSingleton(new RouteConfig
            {
                RouteId = clientName,
                ClusterId = clientName,
                Match = new RouteMatch { Path = $"/{trimmedRootUrl}/{{*any}}" },
                Transforms = new Dictionary<string, string>[]
                {
                    new()
                    {
                        // dit zorgt ervoor dat de gematchde string uit het pad niet in het verzoek van YARP terecht komt
                        // deze komt namelijk niet overeen met het pad binnen de E-Suite.
                        ["PathRemovePrefix"] = $"/{trimmedRootUrl}"
                    },
                    new()
                    {
                        // we rewriten urls tussen de adapter en de esuite.
                        // dat betekent dat de lengte van de body regelmatig niet meer klopt met de Content-Length header
                        // daarom verwijderen we deze header uit de geproxyde verzoeken
                        ["ResponseHeaderRemove"] = "Content-Length"
                    },
                    new()
                    {
                        // YARP voegt standaard een aantal headers toe over waar het oorspronkelijke verzoek vandaan komt
                        // de urls uit de response van de E-Suite worden daardoor verhaspelt.
                        // daarom verwijderen we deze headers uit de geproxyde verzoeken
                        ["X-Forwarded"] = "Remove"
                    }
                }
            });

            services.AddSingleton(s =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseUrl = config.GetRequiredValue("ESUITE_BASE_URL");
                var baseUri = new UriBuilder(baseUrl) { Path = remotePath }.Uri.ToString();

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

            // Naast de configuratie voor YARP hebben we ook een eigen geconfigureerde HttpClient nodig
            // Deze gebruiken we voor calls die niet een op een naar de E-Suite gaan,
            // maar bijvoorbeeld worden gesplits in meerder custom calls
            services.AddHttpClient(clientName, (s, x) =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var urlFromConfig = config.GetRequiredValue("ESUITE_BASE_URL");
                var clientId = config.GetRequiredValue("ESUITE_CLIENT_ID");
                var clientSecret = config.GetRequiredValue("ESUITE_CLIENT_SECRET");
                var token = GetToken(clientId, clientSecret);
                var baseUrl = new UriBuilder(urlFromConfig) { Path = remotePath };
                x.BaseAddress = baseUrl.Uri;
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

        public static void AddEsuiteToken(this IReverseProxyBuilder builder) => builder.AddTransforms(context =>
        {
            context.AddRequestTransform(x =>
            {
                var config = x.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var clientId = config.GetRequiredValue("ESUITE_CLIENT_ID");
                var clientSecret = config.GetRequiredValue("ESUITE_CLIENT_SECRET");
                var token = GetToken(clientId, clientSecret);
                x.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var username = x.HttpContext.User.FindFirstValue("user_id");
                if (!string.IsNullOrWhiteSpace(username))
                {
                    x.ProxyRequest.Headers.Add("X-Request-User-Id", [username]);
                }
                return new ValueTask();
            });
        });

        public static string GetToken(string id, string secret, Dictionary<string,object>? claims = null)
        {
            var now = DateTimeOffset.UtcNow;
            // one minute leeway to account for clock differences between machines
            var issuedAt = now.AddMinutes(-1);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = id,
                IssuedAt = issuedAt.DateTime,
                NotBefore = issuedAt.DateTime,
                Claims = claims ?? new Dictionary<string, object>
                {
                    { "client_id", id },
                },
                Subject = new ClaimsIdentity(),
                Expires = now.AddHours(1).DateTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
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
