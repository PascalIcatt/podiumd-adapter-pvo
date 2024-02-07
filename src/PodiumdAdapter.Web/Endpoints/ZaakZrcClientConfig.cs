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
                Url = "zaken" + request.QueryString,
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
        }

        private static void MapInternZaaknummerToIdentificatie(JsonNode? item)
        {
            if (item?["identificatieIntern"]?.GetValue<string>() is string identificatieIntern
                    && !string.IsNullOrWhiteSpace(identificatieIntern))
            {
                item["identificatie"] = identificatieIntern;
            }
        }
    }
}
