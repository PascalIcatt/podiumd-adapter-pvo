using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using static PodiumdAdapter.Web.Auth.AuthExtensions;

namespace PodiumdAdapter.Web.Endpoints
{
    public static class InterneTaakCustomEndpoints
    {
        const string ApiRoot = "/internetaak/api/v2/objects";

        public static IEndpointConventionBuilder MapInterneTaakCustomEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            var group = endpointRouteBuilder.MapGroup(ApiRoot);

            if (!endpointRouteBuilder.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                group.RequireObjectenApiKey();
            }

            group.MapPost("/", async (HttpRequest request) =>
            {
                var json = await JsonNode.ParseAsync(request.Body);

                if (!TryParseContactmomentId(json, out var contactmomentId))
                {
                    return Results.Problem("contactmoment ontbreekt of is niet valide", statusCode: 400);
                }

                var uriBuilder = new UriBuilder
                {
                    Scheme = request.Scheme,
                    Host = request.Host.Host,
                    Path = ApiRoot + "/" + contactmomentId
                };

                if (request.Host.Port.HasValue)
                {
                    uriBuilder.Port = request.Host.Port.Value;
                }

                var url = uriBuilder.Uri.ToString();

                var response = new JsonObject { ["url"] = url };

                return Results.Ok(response);
            });

            return group;
        }

        private static bool TryParseContactmomentId(JsonNode? json, [NotNullWhen(true)] out string? result)
        {
            result = json?["record"]?["data"]?["contactmoment"]?.GetValue<string>()
                ?.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();

            return !string.IsNullOrWhiteSpace(result);
        }
    }
}
