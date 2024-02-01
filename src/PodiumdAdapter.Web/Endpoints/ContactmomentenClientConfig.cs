using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IESuiteClientConfig
    {
        const string ObjectcontactmomentenKey = "objectcontactmomenten";

        public string ProxyBasePath => "/contactmomenten-api-provider/api/v1";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            clientRoot.MapGet("/contactmomenten", async (HttpRequest request, CancellationToken token) =>
            {
                var client = getClient();

                // in OpenKlant zit een uitbreiding op de contacmomenten standaard.
                // Je kan contactmomenten daarin filteren op objectUrl (in de praktijk is dat de url van de zaak die erbij hoort).
                // dit zit niet in de standaard. Mogelijk wordt dit nog wel in de API van de eSuite geimplementeerd. Dan kan onderstaande code eruit.
                if (TryGetObjectUrlFromQuery(request.Query, out var objectUrl))
                {
                    return await GetContactmomentenFilteredByObjectUrl(client, objectUrl, token);
                }

                var url = "contactmomenten" + (request.QueryString.Value ?? "");

                // in OpenKlant zit een uitbreiding op de contacmomenten standaard.
                // Je kan daarin met een expand parameter aangeven dat je de objectcontactmomenten wil 'uitklappen' in de lijst met contactmomenten.
                // dit zit niet in de standaard. Mogelijk wordt dit nog wel in de API van de eSuite geimplementeerd. Dan kan onderstaande code eruit.
                if (ShouldIncludeObjectContactmomenten(request))
                {
                    return GetContactmomentenWithObjectContactmomenten(client, url, PlakAntwoordPropertyAchterTekstProperty);
                }

                // als je niet wil filteren op objectUrl, en ook de objectContactmomenten niet hoeft uit te klappen, kunnen we het request as-is proxyen
                return GetContactmomentenDefault(client, url, PlakAntwoordPropertyAchterTekstProperty);
            });

            clientRoot.MapPost("/contactmomenten", () =>
            {
                var client = getClient();
                return client.ProxyResult(new ProxyRequest
                {
                    Url = "contactmomenten",
                    ModifyRequestBody = (json, token) =>
                    {
                        var tekst = json["tekst"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(tekst))
                        {
                            json["tekst"] = "X";
                        }
                        if (json["medewerkerIdentificatie"] is JsonObject identificatie)
                        {
                            identificatie["identificatie"] = "Felix";
                        }
                        return new ValueTask();
                    }
                });
            });
        }

        private static bool TryGetObjectUrlFromQuery(IQueryCollection query, out string result)
        {
            if (query.TryGetValue("object", out var objectQuery)
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
            foreach (var item in contactmomenten)
            {
                PlakAntwoordPropertyAchterTekstProperty(item);
            }

            var paginated = new JsonObject
            {
                ["results"] = new JsonArray(contactmomenten),
                ["next"] = null,
                ["previous"] = null,
                ["count"] = contactmomenten.Length,
            };

            return Results.Json(paginated);
        }

        private static void PlakAntwoordPropertyAchterTekstProperty(JsonNode? contactmoment)
        {
            if (contactmoment is JsonObject obj && obj["antwoord"]?.GetValue<string>() is string antwoord)
            {
                var tekst = obj["tekst"]?.GetValue<string>() ?? "";
                obj["tekst"] = string.Join('\n', tekst, antwoord);
            }
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
            && expand.Contains(ObjectcontactmomentenKey);

        private static IResult GetContactmomentenWithObjectContactmomenten(HttpClient client, string url, Action<JsonNode?> modifyContactmoment) => client.ProxyResult(new ProxyRequest
        {
            Url = url,
            Method = HttpMethod.Get,
            ModifyResponseBody = async (json, token) =>
            {
                if (!json.TryParsePagination(out var arr))
                {
                    return;
                }



                var tasks = arr.Select(async (item) =>
                {
                    modifyContactmoment?.Invoke(item);

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
            }
        });

        private static IResult GetContactmomentenDefault(HttpClient client, string url, Action<JsonNode?> modifyContactmoment) => client.ProxyResult(new ProxyRequest
        {
            Url = url,
            ModifyResponseBody = (json, _) =>
            {
                if (json.TryParsePagination(out var page))
                {
                    foreach (var item in page)
                    {
                        modifyContactmoment?.Invoke(item);
                    }
                }
                return new ValueTask();
            }
        });
    }
}
