using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_CONTACTMOMENTEN_BASE_URL";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient) => clientRoot.MapGet("/contactmomenten", (HttpContext context) =>
        {
            var url = "contactmomenten" + (context.Request.QueryString.Value ?? "");
            var client = getClient();
            HttpRequestMessage GetRequest() => new(HttpMethod.Get, url);

            if (!context.Request.Query.TryGetValue("expand", out var expand) || !expand.Contains("objectcontactmomenten"))
            {
                return client.ProxyResult(GetRequest);
            }

            return client.ProxyResult(GetRequest, async (json) =>
            {
                if (!json.TryParsePagination(out var arr))
                {
                    return;
                }

                const string ObjectcontactmomentenKey = "objectcontactmomenten";

                var tasks = arr.Select(async (item) =>
                {
                    if (item is not JsonObject o
                        || o.ContainsKey(ObjectcontactmomentenKey)
                        || !o.TryGetPropertyValue("url", out var u)
                        || u is not JsonValue v)
                    {
                        return;
                    }

                    var objectUrl = "objectcontactmomenten?contactmoment=" + v.ToString();
                    var node = await client.JsonAsync(() => new HttpRequestMessage(HttpMethod.Get, objectUrl));
                    if (node.TryParsePagination(out var arr))
                    {
                        item[ObjectcontactmomentenKey] = arr.DeepClone();
                    }
                });

                await Task.WhenAll(tasks);
            });
        });
    }
}
