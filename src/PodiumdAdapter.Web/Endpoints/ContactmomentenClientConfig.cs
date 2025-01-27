using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IESuiteClientConfig
    {
        const string ObjectcontactmomentenKey = "objectcontactmomenten";
        private const string ContactverzoekStatusValue = "nieuw";
        private const string ContactmomentStatusValue = "afgehandeld";

        public string ProxyBasePath => "/contactmomenten-api-provider/api/v1";

        public string RootUrl => "/contactmomenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            clientRoot.MapGet("/contactmomenten", OphalenContactmomenten(getClient));

            clientRoot.MapPost("/contactmomenten", (IConfiguration config) => OpslaanContactmomentOfContactverzoek(getClient(), config));

            clientRoot.MapGet("/contactmomenten/{id:guid}", (Guid id) => OphalenContactmoment(id, getClient(), PlakAntwoordPropertyAchterTekstProperty));
        }

        private static IResult OphalenContactmoment(Guid id, HttpClient client, Action<JsonNode?> modifyJson) => client.ProxyResult(new ProxyRequest
        {
            Url = "contactmomenten/" + id,
            ModifyResponseBody = (json, _) =>
            {
                modifyJson(json);
                return new ValueTask();
            }
        });

        private static Func<HttpRequest, CancellationToken, Task<IResult>> OphalenContactmomenten(Func<HttpClient> getClient)
        {
            return async (HttpRequest request, CancellationToken token) =>
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
            };
        }

        private static IResult OpslaanContactmomentOfContactverzoek(HttpClient client, IConfiguration configuration)
        {
            var contactverzoekType = configuration.GetSection("CONTACTVERZOEK_TYPES").Get<IEnumerable<string>>()?.Where(x => !string.IsNullOrWhiteSpace(x)).FirstOrDefault();
            return client.ProxyResult(new ProxyRequest
            {
                Url = "contactmomenten",
                ModifyRequestBody = (json, token) =>
                {
                    ModifyPostContactmomentBody(json, contactverzoekType);

                    return new ValueTask();
                }
            });
        }

        public static void ModifyPostContactmomentBody(JsonNode json, string? contactverzoekType)
        {
            // tekst stoppen we in antwoord
            var antwoord = json["tekst"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(antwoord))
            {
                // antwoord is verplicht in de e-suite, maar tekst is niet verplicht in KISS
                antwoord = "N.v.t.";
            }
            json["antwoord"] = antwoord;

            // tekst vervangen met vraag en primaire vraag
            var tekst = string.Join('\n', GetTekstParts(json));
            if (string.IsNullOrWhiteSpace(tekst))
            {
                // tekst is verplicht in de nieuwere versie van de api maar niet in KISS
                tekst = "N.v.t.";
            }
            json["tekst"] = tekst;

            var isContactverzoek = IsContactverzoek(json, out var betrokkene, out var digitaleAdressen);

            if (isContactverzoek)
            {
                HandleContactverzoekToEsuiteMapping(json, contactverzoekType, betrokkene, digitaleAdressen);
            }

            HandleAfdelingGroepMedewerker(json, isContactverzoek);

            json["status"] = isContactverzoek ? ContactverzoekStatusValue : ContactmomentStatusValue;
        }

        private static bool IsContactverzoek(JsonNode json, out JsonObject? contactverzoekBetrokkene, out JsonArray? contactvezoekDigitaleAdressen)
        {
            contactverzoekBetrokkene = default;
            contactvezoekDigitaleAdressen = default;

            if (json is not JsonObject
               || json["betrokkene"] is not JsonObject betrokkene
               || betrokkene["digitaleAdressen"] is not JsonArray digitaleAdressen)
            {
                return false;
            }

            contactverzoekBetrokkene = betrokkene;
            contactvezoekDigitaleAdressen = digitaleAdressen;

            return true;
        }

        public static void HandleContactverzoekToEsuiteMapping(JsonNode json, string? contactverzoekType, JsonObject? betrokkene, JsonArray? digitaleAdressen)
        {

            var organisatie = betrokkene?["organisatie"]?.GetValue<string>();

            var persoonsnaam = betrokkene?["persoonsnaam"];

            var voornaam = persoonsnaam?["voornaam"]?.GetValue<string>();
            var voorvoegselAchternaam = persoonsnaam?["voorvoegselAchternaam"]?.GetValue<string>();
            var achternaam = persoonsnaam?["achternaam"]?.GetValue<string>();

            var email = digitaleAdressen?
                .Where(x => x?["soortDigitaalAdres"]?.GetValue<string>() == "email")
                .Select(x => x?["adres"]?.DeepClone())
                .Where(x => x != null)
                .FirstOrDefault();

            var telefoonnummerEntries = digitaleAdressen?
                .Where(x => x?["soortDigitaalAdres"]?.GetValue<string>() == "telefoonnummer")
                .Select(x => (Adres: x?["adres"]?.DeepClone(), Omschrijving: x?["omschrijving"]?.GetValue<string>()))
                .Where(x => x.Adres != null)
                .Take(2)
                .ToList();

            var telefoon2Toelichting = telefoonnummerEntries?
                .Select(x => x.Omschrijving)
                .ElementAtOrDefault(1);

            var telefoonnummers = telefoonnummerEntries?.Select(x => x.Adres).ToList();
            var telefoonnummer1 = telefoonnummers?.FirstOrDefault();
            var telefoonnummer2 = telefoonnummers?.ElementAtOrDefault(1);

            var toelichting = json["toelichting"]?.GetValue<string>();

            var combinedToelichting = GetToelichting(voornaam, voorvoegselAchternaam, achternaam, organisatie, telefoon2Toelichting, toelichting);

            json["type"] = contactverzoekType;

            json["toelichting"] = combinedToelichting;

            json["contactgegevens"] = new JsonObject
            {
                ["emailadres"] = email,
                ["telefoonnummer"] = telefoonnummer1,
                ["telefoonnummerAlternatief"] = telefoonnummer2
            };
        }

        private static IEnumerable<string> GetTekstParts(JsonNode json)
        {
            var vraag = json["vraag"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(vraag)) yield return vraag;
            var specifiekeVraag = json["specifiekevraag"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(specifiekeVraag)) yield return specifiekeVraag;
        }

        private static string GetToelichting(string? voornaam, string? voorvoegselAchternaam, string? achternaam, string? organisatie, string? toelichtingTelefoonnummer2, string? toelichting)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(voornaam) || !string.IsNullOrWhiteSpace(achternaam) || !string.IsNullOrWhiteSpace(organisatie))
            {
                builder.Append("Contact opnemen met:");
                if (!string.IsNullOrWhiteSpace(voornaam))
                {
                    builder.Append(' ')
                        .Append(voornaam);
                }
                if (!string.IsNullOrWhiteSpace(voorvoegselAchternaam))
                {
                    builder.Append(' ')
                        .Append(voorvoegselAchternaam);
                }
                if (!string.IsNullOrWhiteSpace(achternaam))
                {
                    builder.Append(' ')
                        .Append(achternaam);
                }
                if (!string.IsNullOrWhiteSpace(organisatie))
                {
                    builder.Append(" (")
                        .Append(organisatie)
                        .Append(')');
                }
            }
            if (!string.IsNullOrWhiteSpace(toelichtingTelefoonnummer2))
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }
                builder.Append("Omschrijving tweede telefoonnummer: ")
                    .Append(toelichtingTelefoonnummer2);
            }
            if (!string.IsNullOrWhiteSpace(toelichting))
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }
                builder.Append(toelichting);
            }
            return builder.ToString();
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

            var paginated = contactmomenten.ToPaginatedResult();

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
            => client.GetAllPages("objectcontactmomenten?object=" + objectUrl, token);

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

        private static void HandleAfdelingGroepMedewerker(JsonNode? json, bool isContactverzoek)
        {
            if (json == null) return;
            var actor = json["actor"];

            // als het een contactmoment betreft,
            // dan wordt de afdeling van het contactmoment doorgestuurd als afdeling van het contact 
            // als het een contactverzoek betreft dan doen we niks met de afdeling/groep van het contactmoment
            if (!isContactverzoek)
            {
                AfdelingOvernemenVanHetContactmoment(json);
                return;
            }
           
            var isContactverzoekVoorMedewerker = IsContactverzoekVoorMedewerker(actor);

            // Als het een contactverzoek betreft en er is een medewerker gekozen bij het contactverzoek,
            // .. dan wordt die medewerker doorgestuurd als de behandelaar van het contact            
            if (isContactverzoekVoorMedewerker)
            {
                var username = actor?["identificatie"]?.GetValue<string>() ?? "";
                var behandelaar = json.GetOrSetProperty("behandelaar", () => new JsonObject());
                behandelaar["gebruikersnaam"] = username;
            }

            // Als het een contactverzoek betreft en er is een afdeling/groep gekozen bij het contactverzoek,
            // .. dan wordt die afdeling/groep doorgestuurd als afdeling/groep van het contact
            if (TryGetOrganisatorischeEenheid(actor, isContactverzoekVoorMedewerker, out var value, out var propertyName))
            {
                json[propertyName] = value;
            }
        }


        private static void AfdelingOvernemenVanHetContactmoment(JsonNode? json)
        {
            if (json?["verantwoordelijkeAfdeling"]?.GetValue<string>() is { Length: > 0 } responsibleDepartment)
            {
                json["afdeling"] = responsibleDepartment;
            }
        }

        private static bool TryGetOrganisatorischeEenheid(JsonNode? actor, bool isContactverzoekVoorMedewerker, [NotNullWhen(true)] out string? value, [NotNullWhen(true)] out string? propertyName)
        {
           
            value = GetOrganisatorischeEenheidNaam(actor, isContactverzoekVoorMedewerker);
            propertyName = GetOrganisatorischeEenheidType(actor);

            return !string.IsNullOrWhiteSpace(propertyName) && !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsContactverzoekVoorMedewerker(JsonNode? actor)
        {
            return actor?["soortActor"]?.GetValue<string>() == "medewerker";
        }

        private static string? GetOrganisatorischeEenheidNaam(JsonNode? actor, bool isContactverzoekVoorMedewerker)
        {
            return isContactverzoekVoorMedewerker ? actor?["naamOrganisatorischeEenheid"]?.GetValue<string>() : actor?["naam"]?.GetValue<string>();
        }

        private static string? GetOrganisatorischeEenheidType(JsonNode? actor)
        {
            return actor?["typeOrganisatorischeEenheid"]?.GetValue<string>();
        }
    }
}
