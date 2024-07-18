using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ZaakZrcClientConfig : IESuiteClientConfig
    {
        public string ProxyBasePath => "/zgw-apis-provider/zrc/api/v1";

        public string RootUrl => "/zaken/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            clientRoot.MapGet("/zaken", (HttpRequest request) => getClient().ProxyResult(new ProxyRequest
            {
                Url = "zaken?" + MapQuery(request.Query),
                ModifyResponseBody = (json, _) =>
                {
                    if (json.TryParsePagination(out var page))
                    {
                        foreach (var item in page)
                        {
                            MapInternZaaknummerToIdentificatie(item);
                        }
                    }
                    return new ValueTask();
                }
            }));

            clientRoot.MapGet("/zaken/{id}", (string id) => getClient().ProxyResult(new ProxyRequest
            {
                Url = "zaken/" + id,
                ModifyResponseBody = (json, _) =>
                {
                    MapInternZaaknummerToIdentificatie(json);
                    return new ValueTask();
                }
            }));

            clientRoot.MapPost("/zaakcontactmomenten", static () => Results.NoContent());
        }

        private static void MapInternZaaknummerToIdentificatie(JsonNode? item)
        {
            if (item?["identificatieIntern"]?.GetValue<string>() is string identificatieIntern
                    && !string.IsNullOrWhiteSpace(identificatieIntern))
            {
                item["identificatie"] = identificatieIntern;
            }
        }

        private static string MapQuery(IQueryCollection query)
        {
            var items = query.SelectMany(x => x.Key.Equals("ordering", StringComparison.OrdinalIgnoreCase)
                ? x.Value.OfType<string>().Select(v => v.StartsWith('-')
                    ? $"{x.Key}={v.AsSpan().Slice(1)}_aflopend"
                    : $"{x.Key}={v}_oplopend")
                : x.Value.OfType<string>().Select(v => $"{x.Key}={v}"));

            return string.Join("&", items);
        }
    }
}
