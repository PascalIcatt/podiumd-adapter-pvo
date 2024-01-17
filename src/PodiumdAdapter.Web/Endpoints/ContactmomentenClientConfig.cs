using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;
using static System.Net.Http.HttpMethod;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_CONTACTMOMENTEN_BASE_URL";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            clientRoot.MapGet("/contactmomenten", async (HttpContext context) =>
            {
                var url = "contactmomenten" + (context.Request.QueryString.Value ?? "");
                var client = getClient();
                var GetRequest = () => new HttpRequestMessage(Get, url);

                var needToExpand = context.Request.Query.TryGetValue("expand", out var expand) && expand.Contains("objectcontactmomenten");
                var needToQueryObjectContactmomenten = context.Request.Query.TryGetValue("object", out var objectQuery);

                if (!needToExpand && !needToQueryObjectContactmomenten)
                {
                    return client.ProxyResult(GetRequest);
                }

                if (needToQueryObjectContactmomenten)
                {
                    var objectContactmomenten = await client.JsonAsync(() => new(Get, "objectcontactmomenten?object=" + objectQuery));
                    if (objectContactmomenten.TryParsePagination(out var records, out var next))
                    {
                        IEnumerable<string> GetUrls() => records!
                            .Select(x => x is JsonObject o && o.TryGetPropertyValue("contactmoment", out var r) ? r?.ToString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))!;

                        var momentUrls = GetUrls()
                            .ToList();

                        while (!string.IsNullOrWhiteSpace(next) && (await client.JsonAsync(() => new(Get, next))).TryParsePagination(out records, out next))
                        {
                            momentUrls.AddRange(GetUrls());
                        }

                        if (momentUrls.Count == 0)
                        {
                            return Results.Json(objectContactmomenten);
                        }

                        var all = await Task.WhenAll(momentUrls.Select(x => client.JsonAsync(() => new(Get, x))));
                        var results = new JsonArray(all);
                        var paginated = new JsonObject
                        {
                            ["results"] = results,
                            ["next"] = null,
                            ["previous"] = null,
                            ["count"] = all.Length,
                        };
                        return Results.Json(paginated);
                    }
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
                            || !o.TryGetPropertyValue("url", out var urlProperty)
                            || urlProperty is not JsonValue urlValue)
                        {
                            return;
                        }

                        var objectUrl = "objectcontactmomenten?contactmoment=" + urlValue.ToString();
                        var node = await client.JsonAsync(() => new HttpRequestMessage(Get, objectUrl));
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
}
