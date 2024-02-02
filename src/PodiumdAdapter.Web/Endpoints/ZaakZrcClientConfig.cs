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
                    MapInternZaaknummerToIdentificatie(json);
                    return new ValueTask();
                }
            }));
        }

        private static void MapInternZaaknummerToIdentificatie(JsonNode json)
        {
            if (json.TryParsePagination(out var page))
            {
                foreach (var item in page)
                {
                    if (item?["internZaaknummer"]?.GetValue<string>() is string internZaaknummer
                    && !string.IsNullOrWhiteSpace(internZaaknummer))
                    {
                        item["identificatie"] = internZaaknummer;
                    }
                }
            }
        }
    }
}
