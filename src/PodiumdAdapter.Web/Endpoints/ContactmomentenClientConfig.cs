using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;
using static System.Net.Http.HttpMethod;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_CONTACTMOMENTEN_BASE_URL";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient) => clientRoot.MapGet("/contactmomenten", async (HttpRequest request) =>
        {
            var client = getClient();

            if (TryGetObjectUrl(request, out var objectUrl))
            {
                return await GetFilteredByObject(client, "objectcontactmomenten?object=" + objectUrl);
            }

            var url = "contactmomenten" + (request.QueryString.Value ?? "");

            if (ShouldIncludeObjectContactmomenten(request))
            {
                return GetWithObjectContactmomenten(client, url);
            }

            return client.ProxyResult(url);
        });

        private static bool TryGetObjectUrl(HttpRequest request, out string result)
        {
            if (request.Query.TryGetValue("object", out var objectQuery)
                && objectQuery.FirstOrDefault() is string objectUrl
                && !string.IsNullOrWhiteSpace(objectUrl))
            {
                result = objectUrl;
                return true;
            }

            result = "";
            return false;
        }

        private static async Task<IResult> GetFilteredByObject(HttpClient client, string? url)
        {
            var tasks = new List<Task<JsonNode?>>();

            await foreach (var item in GetAllPages(client, url))
            {
                if (item is JsonObject o && o.TryGetPropertyValue("contactmoment", out var r) && r?.ToString() is string cm)
                {
                    tasks.Add(client.JsonAsync(cm));
                }
            }

            var results = await Task.WhenAll(tasks);

            var paginated = new JsonObject
            {
                ["results"] = new JsonArray(results),
                ["next"] = null,
                ["previous"] = null,
                ["count"] = results.Length,
            };

            return Results.Json(paginated);
        }

        private static async IAsyncEnumerable<JsonNode> GetAllPages(HttpClient client, string? url)
        {
            while (!string.IsNullOrWhiteSpace(url) && (await client.JsonAsync(url)).TryParsePagination(out var records, out url))
            {
                foreach (var item in records)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
        }
        private static bool ShouldIncludeObjectContactmomenten(HttpRequest request) =>
            request.Query.TryGetValue("expand", out var expand)
            && expand.Contains("objectcontactmomenten");

        private static IResult GetWithObjectContactmomenten(HttpClient client, string url) => client.ProxyResult(url, async (json) =>
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
                    || urlProperty is not JsonValue urlValue
                    || urlValue.ToString() is not string u)
                {
                    return;
                }

                var node = await client.JsonAsync("objectcontactmomenten?contactmoment=" + u);
                if (node.TryParsePagination(out var arr))
                {
                    item[ObjectcontactmomentenKey] = arr.DeepClone();
                }
            });

            await Task.WhenAll(tasks);
        });
    }
}
