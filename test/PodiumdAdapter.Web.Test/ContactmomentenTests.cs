using System.Text.Json.Nodes;
using PodiumdAdapter.Web.Endpoints;

namespace PodiumdAdapter.Web.Test;

public class ContactmomentenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Get_contactmomenten_request_is_forwarded_to_expected_Url()
    {

        var esuiteUrl = factory.ESUITE_BASE_URL + "/contactmomenten-api-provider/api/v1/contactmomenten";
        var adapterPath = "/contactmomenten/api/v1/contactmomenten";

        using var client = factory.CreateClient();
        factory.SetZgwToken(client);

        //MockHttpMessageHandler will intercept calls from the Adapter to e-Suite
        //If the adapter behaves as expected
        //esuiteUrl will be called from the adapter
        //if a call to the adapter is made on the adapterUrl

        var requestsToEsuite = factory.MockHttpMessageHandler
            .Expect(HttpMethod.Get, esuiteUrl)
            .Respond("application/json", "{}");

        await client.GetAsync(adapterPath);

        var nrOfCallsToEsuite = factory.MockHttpMessageHandler.GetMatchCount(requestsToEsuite);

        Assert.Equal(1, nrOfCallsToEsuite);

    }


    [Fact]
    public async Task Post_contactmomenten_request_is_forwarded_to_expected_Url()
    {

        var esuiteUrl = factory.ESUITE_BASE_URL + "/contactmomenten-api-provider/api/v1/contactmomenten";
        var adapterPath = "/contactmomenten/api/v1/contactmomenten";

        using var client = factory.CreateClient();
        factory.SetZgwToken(client);

        //MockHttpMessageHandler will intercept calls from the Adapter to e-Suite
        //If the adapter behaves as expected
        //esuiteUrl will be called from the adapter
        //if a call to the adapter is made on the adapterUrl

        var requestsToEsuite = factory.MockHttpMessageHandler
            .Expect(HttpMethod.Post, esuiteUrl)
            .Respond("application/json", "{}");

        await client.PostAsync(adapterPath, new StringContent("{}"));

        var nrOfCallsToEsuite = factory.MockHttpMessageHandler.GetMatchCount(requestsToEsuite);

        Assert.Equal(1, nrOfCallsToEsuite);

    }

    [Fact]
    public void Contactverzoek_is_mapped_correctly()
    {
        const string InputJson = """
            {
                "bronorganisatie": "999990639",
                "registratiedatum": "2024-02-05T15:59:12.584Z",
                "kanaal": "contactformulier",
                "tekst": "notitie",
                "onderwerpLinks": [],
                "initiatiefnemer": "klant",
                "specifiekevraag": "specifieke vraag",
                "gespreksresultaat": "Contactverzoek gemaakt",
                "voorkeurskanaal": "",
                "voorkeurstaal": "",
                "medewerker": "",
                "startdatum": "2024-02-05T15:32:23.320Z",
                "verantwoordelijkeAfdeling": "Beheer openbare ruimte",
                "einddatum": "2024-02-05T15:59:12.584Z",
                "status": "te verwerken",
                "toelichting": "interne toelichting",
                "actor": {
                    "identificatie": "bo-handhaving",
                    "naam": "Handhaving",
                    "soortActor": "organisatorische eenheid",
                    "typeOrganisatorischeEenheid": "afdeling"
                },
                "betrokkene": {
                    "rol": "klant",
                    "persoonsnaam": {
                        "voornaam": "Voor",
                        "voorvoegselAchternaam": "tussen",
                        "achternaam": "Achter"
                    },
                    "organisatie": "Org",
                    "digitaleAdressen": [
                        {
                            "adres": "icatttest@gmail.com",
                            "omschrijving": "e-mailadres",
                            "soortDigitaalAdres": "email"
                        },
                        {
                            "adres": "0201234567",
                            "omschrijving": "telefoonnummer",
                            "soortDigitaalAdres": "telefoonnummer"
                        },
                        {
                            "adres": "0207654321",
                            "omschrijving": "werk",
                            "soortDigitaalAdres": "telefoonnummer"
                        }
                    ]
                }
            }
            """;

        const string ExpectedResult = """
            {
              "bronorganisatie": "999990639",
              "registratiedatum": "2024-02-05T15:59:12.584Z",
              "kanaal": "contactformulier",
              "tekst": "specifieke vraag",
              "onderwerpLinks": [],
              "initiatiefnemer": "klant",
              "specifiekevraag": "specifieke vraag",
              "gespreksresultaat": "Contactverzoek gemaakt",
              "voorkeurskanaal": "",
              "voorkeurstaal": "",
              "medewerker": "",
              "startdatum": "2024-02-05T15:32:23.320Z",
              "verantwoordelijkeAfdeling": "Beheer openbare ruimte",
              "einddatum": "2024-02-05T15:59:12.584Z",
              "status": "nieuw",
              "toelichting": "Contact opnemen met: Voor tussen Achter (Org)\nOmschrijving tweede telefoonnummer: werk\ninterne toelichting",
              "actor": {
                "identificatie": "bo-handhaving",
                "naam": "Handhaving",
                "soortActor": "organisatorische eenheid",
                "typeOrganisatorischeEenheid": "afdeling"
              },
              "betrokkene": {
                "rol": "klant",
                "persoonsnaam": {
                  "voornaam": "Voor",
                  "voorvoegselAchternaam": "tussen",
                  "achternaam": "Achter"
                },
                "organisatie": "Org",
                "digitaleAdressen": [
                  {
                    "adres": "icatttest@gmail.com",
                    "omschrijving": "e-mailadres",
                    "soortDigitaalAdres": "email"
                  },
                  {
                    "adres": "0201234567",
                    "omschrijving": "telefoonnummer",
                    "soortDigitaalAdres": "telefoonnummer"
                  },
                  {
                    "adres": "0207654321",
                    "omschrijving": "werk",
                    "soortDigitaalAdres": "telefoonnummer"
                  }
                ]
              },
              "antwoord": "notitie",
              "type": "my-type",
              "contactgegevens": {
                "emailadres": "icatttest@gmail.com",
                "telefoonnummer": "0201234567",
                "telefoonnummerAlternatief": "0207654321"
              },
              "afdeling": "Handhaving"
            }
            """;

        var parsed = JsonNode.Parse(InputJson)!;

        ContactmomentenClientConfig.ModifyPostContactmomentBody(parsed, "my-type");

        var result = parsed.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n");

        Assert.Equal(ExpectedResult, result);
    }
}
