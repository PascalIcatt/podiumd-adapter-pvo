using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IESuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_CONTACTMOMENTEN_BASE_URL";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient) => clientRoot.MapGet("/contactmomenten", async (HttpRequest request, CancellationToken token) =>
        {
            var client = getClient();

            // in OpenKlant zit een uitbreiding op de contacmomenten standaard.
            // Je kan contactmomenten daarin filteren op objectUrl (in de praktijk is dat de url van de zaak die erbij hoort).
            // dit zit niet in de standaard. Mogelijk wordt dit nog wel in de API van de eSuite geimplementeerd. Dan kan onderstaande code eruit.
            if (TryGetObjectUrlFromQuery(request, out var objectUrl))
            {
                return await GetContactmomentenFilteredByObjectUrl(client, objectUrl, token);
            }

            var url = "contactmomenten" + (request.QueryString.Value ?? "");

            // in OpenKlant zit een uitbreiding op de contacmomenten standaard.
            // Je kan daarin met een expand parameter aangeven dat je de objectcontactmomenten wil 'uitklappen' in de lijst met contactmomenten.
            // dit zit niet in de standaard. Mogelijk wordt dit nog wel in de API van de eSuite geimplementeerd. Dan kan onderstaande code eruit.
            if (ShouldIncludeObjectContactmomenten(request))
            {
                return GetContactmomentenWithObjectContactmomenten(client, url);
            }

            // als je niet wil filteren op objectUrl, en ook de objectContactmomenten niet hoeft uit te klappen, kunnen we het request as-is proxyen
            return client.ProxyResult(url);
        });

        private static bool TryGetObjectUrlFromQuery(HttpRequest request, out string result)
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

        private static async Task<IResult> GetContactmomentenFilteredByObjectUrl(HttpClient client, string objectUrl, CancellationToken token)
        {
            var contactmomentTasks = new List<Task<JsonNode?>>();

            await foreach (var objectContactmoment in GetObjectContactmomenten(client, objectUrl, token))
            {
                if (objectContactmoment is JsonObject o
                    && o.TryGetPropertyValue("contactmoment", out var r)
                    && r?.ToString() is string contactmomentUrl)
                {
                    var contactmomentTask = client.JsonAsync(contactmomentUrl, token);
                    contactmomentTasks.Add(contactmomentTask);
                }
            }

            var contactmomenten = await Task.WhenAll(contactmomentTasks);

            var paginated = new JsonObject
            {
                ["results"] = new JsonArray(contactmomenten),
                ["next"] = null,
                ["previous"] = null,
                ["count"] = contactmomenten.Length,
            };

            return Results.Json(paginated);
        }

        private static IAsyncEnumerable<JsonNode?> GetObjectContactmomenten(HttpClient client, string objectUrl, CancellationToken token)
            => GetAllPages(client, "objectcontactmomenten?object=" + objectUrl, token);

        private static async IAsyncEnumerable<JsonNode?> GetAllPages(HttpClient client, string? url, [EnumeratorCancellation] CancellationToken token)
        {
            while (!string.IsNullOrWhiteSpace(url) && (await client.JsonAsync(url, token)).TryParsePagination(out var records, out url))
            {
                foreach (var item in records)
                {
                    yield return item;
                }
            }
        }
        private static bool ShouldIncludeObjectContactmomenten(HttpRequest request) =>
            request.Query.TryGetValue("expand", out var expand)
            && expand.Contains("objectcontactmomenten");

        private static IResult GetContactmomentenWithObjectContactmomenten(HttpClient client, string url) => client.ProxyResult(url, async (json, token) =>
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

                var node = await client.JsonAsync("objectcontactmomenten?contactmoment=" + u, token);
                if (node.TryParsePagination(out var arr))
                {
                    item[ObjectcontactmomentenKey] = arr.DeepClone();
                }
            });

            await Task.WhenAll(tasks);
        });
    }
}
