using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints.ObjectenEndpoints
{
    public static class ObjectenEndpoints
    {
        const string ApiRoot = "/api/v2/objects";
        private const string GroepPrefix = "groep:";
        private const string AfdelingPrefix = "afdeling:";
        private const string GroepenClientName = "groepen";
        private const string AfdelingenClientName = "afdelingen";
        private const string SmoelenboekClientName = "smoelenboek";

        private static ILogger? _logger;

        public static void InitializeLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger("ObjectenEndpoints");
        }

        public static IEndpointConventionBuilder MapObjectenEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            var group = endpointRouteBuilder.MapGroup(ApiRoot);

            group.MapPost("/", OpslaanInterneTaakStub);

            group.MapGet("/", GetObjecten);

            return group;
        }

        public static void AddSmoelenboekClient(this IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient(SmoelenboekClientName, (client) =>
            {
                var baseUrl = config.GetRequiredValue("SMOELENBOEK_BASE_URL");
                var token = config.GetRequiredValue("SMOELENBOEK_TOKEN");
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
            });
        }

        // Haalt objecten op van de volgende types:
        // InterneTaak
        // Afdeling
        // Groep
        private static IResult GetObjecten(
            IConfiguration configuration,
            IHttpClientFactory factory,
            HttpRequest request,
            [FromQuery(Name = "data_attrs")] string[] filterMultipleAttributes,
            [FromQuery(Name = "data_attr")] string[] filterSingleAttribute,
            [FromQuery(Name = "type")] string objectType,
            CancellationToken cancellationToken)
        {
            var interneTaakType = configuration.GetRequiredValue("INTERNE_TAAK_OBJECT_TYPE_URL");
            var smoelenboekType = configuration.GetRequiredValue("SMOELENBOEK_OBJECT_TYPE_URL");

            // voor interne taak gaan we naar de contactmomenten api van de esuite
            if (objectType == interneTaakType)
            {
                var filterAttributes = filterMultipleAttributes.Concat(filterSingleAttribute).ToArray();
                return GetInterneTaken(configuration, factory, request, filterAttributes, objectType);
            }

            if (objectType == smoelenboekType)
            {
                return GetSmoelenboek(factory, request);
            }

            return Results.Problem("objecttype onbekend: " + objectType, statusCode: StatusCodes.Status400BadRequest);
        }

        //wordt gebruikt door de elastic sync om de gegevens,
        //zoals ze door de esuite in overige objecten zijn gezet,
        //aan te passen zodat ze in de elastic index terecht komen op de manier dat kiss verwacht
        private static IResult GetSmoelenboek(IHttpClientFactory factory, HttpRequest request)
        {
            _logger?.LogInformation("GetSmoelenboek is aangeroepen voor elastic sync aanpassingen.");

            var client = factory.CreateClient(SmoelenboekClientName);
            return client.ProxyResult(new ProxyRequest
            {
                Url = request.Path + request.QueryString,
                ModifyResponseBody = (json, _) =>
                {
                    if (json.TryParsePagination(out var page))
                    {
                        foreach (var item in page)
                        {
                            if (item?["record"]?["data"] is JsonObject data
                                && data["volledigeNaam"]?.GetValue<string>() is string volledigeNaam
                                && !string.IsNullOrWhiteSpace(volledigeNaam))
                            {
                                data["achternaam"] = volledigeNaam;
                            }
                        }
                    }
                    return new ValueTask();
                }
            });
        }

        private static IResult GetInterneTaken(IConfiguration configuration, IHttpClientFactory factory, HttpRequest request, string[] filterAttributes, string? objectType)
        {
            var types = configuration.GetSection("CONTACTVERZOEK_TYPES")?.Get<IEnumerable<string>>()?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
            if (types.Length == 0) return Results.Problem("Het type contact dat hoort bij een Contactverzoek is niet opgenomen in de instellingen van de adapter. Neem contact op met een beheerder", statusCode: 500);

            var klant = ExtractFilterAttribute(filterAttributes, "betrokkene__klant__exact__");
            var digitaleAdressen = ExtractFilterAttribute(filterAttributes, "betrokkene__digitaleAdressen__icontains__");

            var client = factory.CreateClient(nameof(ContactmomentenClientConfig));

            var builder = new QueryBuilder
            {
                { "type", types }
            };

            if (request.Query.TryGetValue("page", out var page))
            {
                builder.Add("page", page.AsEnumerable().OfType<string>());
            }

            if (!string.IsNullOrWhiteSpace(klant))
            {
                builder.Add("klant", klant);
            }

            if (!string.IsNullOrWhiteSpace(digitaleAdressen))
            {
                builder.Add("telefoonnummerOfEmailadres", digitaleAdressen);
            }

            var queryString = builder.ToQueryString().Value ?? "";

            return client.ProxyResult(new ProxyRequest
            {
                Url = "contactmomenten" + queryString,
                ModifyResponseBody = (json, cancellationToken) => MapContactmomentenResponseToContactverzoeken(json, request, client, klant, objectType, cancellationToken)
            });
        }

        private static string? ExtractFilterAttribute(string[] filterAttributes, string prefix)
        {
            return filterAttributes
                .Where(x => x.StartsWith(prefix))
                .Select(x => x.Substring(prefix.Length))
                .FirstOrDefault();
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
            var actorGebruikersnaam = behandelaar?["gebruikersnaam"]?.GetValue<string>();
            var afdeling = obj["afdeling"]?.GetValue<string>();
            var groep = obj["groep"]?.GetValue<string>();

            JsonObject? actor = null;

            if (!string.IsNullOrWhiteSpace(actorGebruikersnaam))
            {
                actor = new JsonObject
                {
                    ["naam"] = actorNaam,
                    ["soortActor"] = "medewerker",
                    ["identificatie"] = actorGebruikersnaam
                };
            }
            else if (!string.IsNullOrWhiteSpace(groep))
            {
                groep = GroepPrefix + groep;
                actor = new JsonObject
                {
                    ["naam"] = groep,
                    ["identificatie"] = groep,
                    ["soortActor"] = "organisatorische eenheid"
                };
            }
            else if (!string.IsNullOrWhiteSpace(afdeling))
            {
                afdeling = AfdelingPrefix + afdeling;
                actor = new JsonObject
                {
                    ["naam"] = afdeling,
                    ["identificatie"] = afdeling,
                    ["soortActor"] = "organisatorische eenheid"
                };
            }

            var toelichting = UpdateToelichtingWithRecentsteVoorlopigAntwoord(obj, contact);
            var status = obj["status"]?.DeepClone();
            var registratieDatum = obj["registratiedatum"]?.DeepClone();
            var digitaleAdressen = GetDigitaleAdressen(obj);

            var betrokkene = new JsonObject
            {
                ["rol"] = "klant",
                ["digitaleAdressen"] = digitaleAdressen
            };

            if (klant != null)
            {
                betrokkene["klant"] = klant;
            }

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
                    ["actor"] = actor,
                    ["status"] = status,
                    ["betrokkene"] = betrokkene,

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
                    ["soortDigitaalAdres"] = "email"
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

        private static string UpdateToelichtingWithRecentsteVoorlopigAntwoord(JsonObject obj, JsonNode? contact)
        {
            var bestaandeToelichting = obj["toelichting"]?.GetValue<string>() ?? "";

            var recentsteVoorlopigAntwoord = HaalLaatsteVoorlopigeAntwoordOp(contact);
            var updatedToelichting = string.IsNullOrWhiteSpace(bestaandeToelichting)
                                             ? recentsteVoorlopigAntwoord
                                             : string.Concat(bestaandeToelichting, "\n\n", recentsteVoorlopigAntwoord).Trim();

            return updatedToelichting;
        }

        private static string HaalLaatsteVoorlopigeAntwoordOp(JsonNode? contactmoment)
        {
            if (contactmoment?["recentsteVoorlopigAntwoord"] is JsonObject recentsteVoorlopigAntwoord
                && recentsteVoorlopigAntwoord["antwoord"]?.GetValue<string>() is string antwoord
                && recentsteVoorlopigAntwoord["volledigeNaam"]?.GetValue<string>() is string volledigeNaam
                && recentsteVoorlopigAntwoord["registratiedatum"]?.GetValue<string>() is string registratieDatum)
            {
                var dateTimeOffset = DateTimeOffset.Parse(registratieDatum);
                var formattedDate = dateTimeOffset.ToString("dd-MM-yyyy, HH:mm");

                return $"Laatste voorlopige antwoord: {antwoord} ({formattedDate}, {volledigeNaam})";
            }

            return string.Empty;
        }
    }
}
