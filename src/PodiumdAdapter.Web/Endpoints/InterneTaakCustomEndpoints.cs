using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using PodiumdAdapter.Web.Auth;

namespace PodiumdAdapter.Web.Endpoints
{
    public static class InterneTaakCustomEndpoints
    {
        const string ApiRoot = "/api/v2/objects";

        public static IEndpointConventionBuilder MapInterneTaakCustomEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            var group = endpointRouteBuilder.MapGroup(ApiRoot);

            if (!endpointRouteBuilder.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                group.RequireObjectenApiKey();
            }

            group.MapPost("/", OpslaanInterneTaakStub);

            group.MapGet("/", GetInterneTaken);

            return group;
        }

        private static IResult GetInterneTaken(
            IConfiguration configuration,
            IHttpClientFactory factory,
            HttpRequest request,
            [FromQuery(Name = "data_attrs")] string[] filterAttributes,
            [FromQuery(Name = "type")] string? objectType)
        {
            var types = configuration.GetSection("CONTACTVERZOEK_TYPES")?.Get<IEnumerable<string>>()?.Where(x=> !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
            if (types.Length == 0) return Results.Problem("Het type contact dat hoort bij een Contactverzoek is niet opgenomen in de instellingen van de adapter. Neem contact op met een beheerder", statusCode: 500);

            var klant = filterAttributes
                .Select(x => x.Split("betrokkene__klant__exact__", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .FirstOrDefault();

            var client = factory.CreateClient(nameof(ContactmomentenClientConfig));

            var builder = new QueryBuilder
            {
                { "type", types }
            };

            if (!string.IsNullOrWhiteSpace(klant))
            {
                builder.Add("klant", klant);
            }

            var queryString = builder.ToQueryString().Value ?? "";

            return client.ProxyResult(new ProxyRequest
            {
                Url = "contactmomenten" + queryString,
                ModifyResponseBody = (json, cancellationToken) => MapContactmomentenResponseToContactverzoeken(json, request, client, klant, objectType, cancellationToken)
            });
        }

        private static async ValueTask MapContactmomentenResponseToContactverzoeken(
            JsonNode? json,
            HttpRequest request,
            HttpClient client,
            string? klant,
            string? objectType,
            CancellationToken cancellationToken)
        {
            if (!json.TryParsePagination(out var page))
            {
                return;
            }

            var tasks = page.Select(contact => MapContactverzoek(contact, request, client, klant, objectType, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private static async Task MapContactverzoek(JsonNode? contact, HttpRequest request, HttpClient client, string? klant, string? objectType, CancellationToken cancellationToken)
        {
            if (contact is not JsonObject obj)
            {
                return;
            }

            var cmUrl = obj["url"]?.GetValue<string>() ?? "";

            if (string.IsNullOrWhiteSpace(klant))
            {
                klant = await GetKlantUrl(client, cmUrl, cancellationToken);
            }

            var uuid = cmUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var taakUrl = GetInterneTaakUrl(request, uuid);
            var medewerkerIdentificatie = obj["medewerkerIdentificatie"]?.DeepClone();
            var behandelaar = obj["behandelaar"];
            var actorNaam = behandelaar?["volledigeNaam"]?.DeepClone();
            var actorGebruikersnaam = behandelaar?["gebruikersnaam"]?.DeepClone();
            var toelichting = behandelaar?["toelichting"]?.DeepClone();
            var status = obj["status"]?.DeepClone();
            var registratieDatum = obj["registratiedatum"]?.DeepClone();
            var digitaleAdressen = GetDigitaleAdressen(obj);

            obj.Clear();
            obj["url"] = taakUrl;
            obj["uuid"] = uuid;
            obj["type"] = objectType;
            obj["record"] = new JsonObject
            {
                ["index"] = 1,
                ["typeVersion"] = 1,
                ["data"] = new JsonObject
                {
                    ["actor"] = actorGebruikersnaam == null
                        ? null
                        : new JsonObject
                        {
                            ["naam"] = actorNaam,
                            ["soortActor"] = "medewerker",
                            ["identificatie"] = actorGebruikersnaam
                        },
                    ["status"] = status,
                    ["betrokkene"] = new JsonObject
                    {
                        ["rol"] = "klant",
                        ["klant"] = klant,
                        ["digitaleAdressen"] = digitaleAdressen
                    },

                    ["toelichting"] = toelichting,
                    ["contactmoment"] = cmUrl,
                    ["registratiedatum"] = registratieDatum,
                    ["medewerkerIdentificatie"] = medewerkerIdentificatie,
                }
            };
        }

        private static async Task<IResult> OpslaanInterneTaakStub(HttpRequest request)
        {
            var json = await JsonNode.ParseAsync(request.Body);

            if (!TryParseContactmomentId(json, out var contactmomentId))
            {
                return Results.Problem("contactmoment ontbreekt of is niet valide", statusCode: 400);
            }

            var response = json.DeepClone();
            response["url"] = GetInterneTaakUrl(request, contactmomentId);
            response["uuid"] = contactmomentId;

            return Results.Ok(response);
        }

        private static async Task<string?> GetKlantUrl(HttpClient client, string cmUrl, CancellationToken cancellationToken)
        {
            var klantenJson = await client.JsonAsync("klantcontactmomenten?contactmoment=" + cmUrl, cancellationToken);
            
            if (klantenJson.TryParsePagination(out var klantPage))
            {
               return klantPage.Select(x => x?["klant"]?.GetValue<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .FirstOrDefault();
            }

            return null;
        }

        private static JsonArray GetDigitaleAdressen(JsonObject obj)
        {
            var contactgegevens = obj["contactgegevens"];
            var email = contactgegevens?["emailadres"]?.DeepClone();
            var telefoonnummer1 = contactgegevens?["telefoonnummer"]?.DeepClone();
            var telefoonnummer2 = contactgegevens?["telefoonnummerAlternatief"]?.DeepClone();
            var digitaleAdressen = new JsonArray();
            if (email != null)
            {
                digitaleAdressen.Add(new JsonObject
                {
                    ["adres"] = email,
                    ["omschrijving"] = "e-mailadres",
                    ["soortDigitaalAdres"] = "e-mailadres"
                });
            }
            if (telefoonnummer1 != null)
            {
                digitaleAdressen.Add(new JsonObject
                {
                    ["adres"] = telefoonnummer1,
                    ["omschrijving"] = "telefoonnummer",
                    ["soortDigitaalAdres"] = "telefoonnummer"
                });
            }
            if (telefoonnummer2 != null)
            {
                digitaleAdressen.Add(new JsonObject
                {
                    ["adres"] = telefoonnummer2,
                    ["omschrijving"] = "alternatief telefoonnummer",
                    ["soortDigitaalAdres"] = "telefoonnummer"
                });
            }

            return digitaleAdressen;
        }

        private static string GetInterneTaakUrl(HttpRequest request, string contactmomentId)
        {
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
            return url;
        }

        private static bool TryParseContactmomentId([NotNullWhen(true)] JsonNode? json, [NotNullWhen(true)] out string? result)
        {
            result = json?["record"]?["data"]?["contactmoment"]?.GetValue<string>()
                ?.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();

            return !string.IsNullOrWhiteSpace(result);
        }
    }
}
