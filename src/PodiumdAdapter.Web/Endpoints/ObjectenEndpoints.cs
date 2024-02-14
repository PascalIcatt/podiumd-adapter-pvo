using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using PodiumdAdapter.Web.Auth;

namespace PodiumdAdapter.Web.Endpoints
{
    public static class ObjectenEndpoints
    {
        const string ApiRoot = "/api/v2/objects";

        public static IEndpointConventionBuilder MapObjectenEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            var group = endpointRouteBuilder.MapGroup(ApiRoot);

            if (!endpointRouteBuilder.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                group.RequireObjectenApiKey();
            }

            group.MapPost("/", OpslaanInterneTaakStub);

            group.MapGet("/", GetObjecten);

            return group;
        }

        public static void AddAfdelingenClient(this IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient("afdelingen", (client) =>
            {
                var baseUrl = config["AFDELINGEN_BASE_URL"];
                var token = config["AFDELINGEN_TOKEN"];
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
            });
        }

        public static void AddGroepenClient(this IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient("groepen", (client) =>
            {
                var baseUrl = config["GROEPEN_BASE_URL"];
                var token = config["GROEPEN_TOKEN"];
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
            });
        }

        private static async Task<IResult> GetObjecten(
            IConfiguration configuration,
            IHttpClientFactory factory,
            HttpRequest request,
            [FromQuery(Name = "data_attrs")] string[] filterAttributes,
            [FromQuery(Name = "type")] string objectType,
            CancellationToken cancellationToken)
        {
            var interneTaakType = configuration["INTERNE_TAAK_OBJECT_TYPE_URL"];
            if (objectType == interneTaakType)
            {
                return GetInterneTaken(configuration, factory, request, filterAttributes, objectType);
            }
            var groepenType = configuration["GROEPEN_OBJECT_TYPE_URL"];
            var afdelingenType = configuration["AFDELINGEN_OBJECT_TYPE_URL"];

            if (objectType != afdelingenType)
            {
                return Results.Problem("type onbekend", statusCode: StatusCodes.Status400BadRequest);
            }

            return await GetAfdelingenEnGroepen(factory, request, afdelingenType, groepenType, cancellationToken);
        }

        private static async Task<IResult> GetAfdelingenEnGroepen(IHttpClientFactory factory, HttpRequest request, string afdelingenType, string groepenType, CancellationToken cancellationToken)
        {
            var afdelingenClient = factory.CreateClient("afdelingen");
            var groepenClient = factory.CreateClient("groepen");

            var groepenQuery = request.QueryString.Value?.Replace(afdelingenType, groepenType);
            var afdelingenUrl = request.Path + request.QueryString;
            var groepenUrl = request.Path + groepenQuery;

            var afdelingenTask = afdelingenClient.GetAllPages(afdelingenUrl, cancellationToken).ToListAsync(cancellationToken);
            var groepenTask = groepenClient.GetAllPages(groepenUrl, cancellationToken).ToListAsync(cancellationToken);
            var afdelingen = await afdelingenTask;
            var groepen = await groepenTask;

            var all = afdelingen.Concat(groepen).Select(x =>
            {
                if (x == null) return null;
                var json = x.DeepClone();
                var type = json["type"]?.GetValue<string>();
                json["type"] = afdelingenType;
                if (json["record"]?["data"] is JsonObject data
                && data["naam"]?.GetValue<string>() is string naam
                && !string.IsNullOrWhiteSpace(naam))
                {
                    var prefix = type == afdelingenType
                        ? "afdeling:"
                        : "groep:";
                    data["naam"] = prefix + naam;
                }
                return json;
            }).ToArray();
            var result = all.ToPaginatedResult();

            return Results.Json(result);
        }

        private static IResult GetInterneTaken(IConfiguration configuration, IHttpClientFactory factory, HttpRequest request, string[] filterAttributes, string? objectType)
        {
            var types = configuration.GetSection("CONTACTVERZOEK_TYPES")?.Get<IEnumerable<string>>()?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
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
                actor = new JsonObject
                {
                    ["naam"] = groep,
                    ["identificatie"] = groep,
                    ["soortActor"] = "organisatorische eenheid"
                };
            }
            else if (!string.IsNullOrWhiteSpace(afdeling))
            {
                actor = new JsonObject
                {
                    ["naam"] = afdeling,
                    ["identificatie"] = afdeling,
                    ["soortActor"] = "organisatorische eenheid"
                };
            }

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
                    ["actor"] = actor,
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
