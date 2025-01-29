using System.Net.Http.Json;

namespace PodiumdAdapter.Web.Test
{
    public class InterneTaakTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        const string BaseUrl = "/api/v2/objects";

        [Fact]
        public async Task Post_response_contains_correct_url()
        {
            var cmId = Guid.NewGuid().ToString();
            var cmUrl = "https://www.google.nl/" + cmId;
            var expectedContent = $$$"""
            {"record":{"data":{"contactmoment":"{{{cmUrl}}}"}},"url":"http://localhost{{{BaseUrl}}}/{{{cmId}}}","uuid":"{{{cmId}}}"}
            """;

            using var client = factory.CreateClient();
            factory.SetZgwToken(client);

            using var response = await client.PostAsJsonAsync(BaseUrl, new
            {
                record = new
                {
                    data = new
                    {
                        contactmoment = cmUrl
                    }
                }
            });

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public async Task Get_internetaak_mapping_is_implemented_correctly()
        {
            const string ApiResponse = """
                {"results":[{"afdeling":"Algemeen","behandelaar":{"gebruikersnaam":"Gebruikersnaam","volledigeNaam":"Voornaam Achternaam"},"toelichting":"toelichting","bronorganisatie":"CHANGEME","contactgegevens":{"emailadres":"admin@example.com","telefoonnummer":"0612345678", "telefoonnummerAlternatief": "067654321"},"identificatie":"73-2024","kanaal":"contactformulier","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"},"onderwerp":"afd:Algemeen","registratiedatum":"2024-02-01T12:21:35+01:00","status":"nieuw","tekst":"vraag","type":"Terugbelverzoek","url":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","objectcontactmomenten":[]}]}
                """;

            const string ExpectedResult = """
                {"results":[{"url":"http://localhost/api/v2/objects/adc06246-4143-449e-a79a-3623442b24a0","uuid":"adc06246-4143-449e-a79a-3623442b24a0","type":"my-type","record":{"index":1,"typeVersion":1,"data":{"actor":{"naam":"Voornaam Achternaam","soortActor":"medewerker","identificatie":"Gebruikersnaam"},"status":"nieuw","betrokkene":{"rol":"klant","digitaleAdressen":[{"adres":"admin@example.com","omschrijving":"e-mailadres","soortDigitaalAdres":"email"},{"adres":"0612345678","omschrijving":"telefoonnummer","soortDigitaalAdres":"telefoonnummer"},{"adres":"067654321","omschrijving":"alternatief telefoonnummer","soortDigitaalAdres":"telefoonnummer"}]},"toelichting":"toelichting","contactmoment":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","registratiedatum":"2024-02-01T12:21:35+01:00","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"}}}}]}
                """;

            factory.MockHttpMessageHandler
                .When("*")
                .Respond("application/json", ApiResponse);

            using var client = factory.CreateClient();
            factory.SetZgwToken(client);

            var str = await client.GetStringAsync(BaseUrl + "?type=" + factory.INTERNE_TAAK_OBJECT_TYPE_URL);
            Assert.Equal(ExpectedResult, str);
        }

        [Theory]
        [InlineData("data_attr=betrokkene__klant__exact__", "klant")]
        [InlineData("data_attrs=betrokkene__klant__exact__", "klant")]
        [InlineData("data_attr=betrokkene__digitaleAdressen__icontains__", "telefoonnummerOfEmailadres")]
        [InlineData("data_attrs=betrokkene__digitaleAdressen__icontains__", "telefoonnummerOfEmailadres")]
        public async Task Get_internetaak_filter_is_implemented_correctly(string queryIn, string queryOut)
        {
            const string ApiResponse = """
                {"results":[{"afdeling":"Algemeen","behandelaar":{"gebruikersnaam":"Gebruikersnaam","volledigeNaam":"Voornaam Achternaam"},"toelichting":"toelichting","bronorganisatie":"CHANGEME","contactgegevens":{"emailadres":"admin@example.com","telefoonnummer":"0612345678", "telefoonnummerAlternatief": "067654321"},"identificatie":"73-2024","kanaal":"contactformulier","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"},"onderwerp":"afd:Algemeen","registratiedatum":"2024-02-01T12:21:35+01:00","status":"nieuw","tekst":"vraag","type":"Terugbelverzoek","url":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","objectcontactmomenten":[]}]}
                """;
            var queryValue = "12345";
            var esuiteUrl = $"{factory.ESUITE_BASE_URL}/contactmomenten-api-provider/api/v1/contactmomenten?type={factory.CONTACTVERZOEK_OBJECT_TYPE_URL}&{queryOut}={queryValue}";
            factory.MockHttpMessageHandler
                .Expect(esuiteUrl)
                .Respond("application/json", ApiResponse);

            using var client = factory.CreateClient();
            factory.SetZgwToken(client);

            using var response = await client.GetAsync($"{BaseUrl}?type={factory.INTERNE_TAAK_OBJECT_TYPE_URL}&{queryIn}{queryValue}");
            Assert.True(response.IsSuccessStatusCode);
        }

        [Theory]
        [InlineData("&page=1", "&page=1")]
        [InlineData("&page=2", "&page=2")]
        [InlineData("&page", "")]
        [InlineData("", "")]
        public async Task Get_internetaak_pagination_is_implemented_correctly(string queryIn, string queryOut)
        {
            const string ApiResponse = """
                {"results":[{"afdeling":"Algemeen","behandelaar":{"gebruikersnaam":"Gebruikersnaam","volledigeNaam":"Voornaam Achternaam"},"toelichting":"toelichting","bronorganisatie":"CHANGEME","contactgegevens":{"emailadres":"admin@example.com","telefoonnummer":"0612345678", "telefoonnummerAlternatief": "067654321"},"identificatie":"73-2024","kanaal":"contactformulier","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"},"onderwerp":"afd:Algemeen","registratiedatum":"2024-02-01T12:21:35+01:00","status":"nieuw","tekst":"vraag","type":"Terugbelverzoek","url":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","objectcontactmomenten":[]}]}
                """;
            var esuiteUrl = $"{factory.ESUITE_BASE_URL}/contactmomenten-api-provider/api/v1/contactmomenten?type={factory.CONTACTVERZOEK_OBJECT_TYPE_URL}{queryOut}";
            factory.MockHttpMessageHandler
                .Expect(esuiteUrl)
                .Respond("application/json", ApiResponse);

            using var client = factory.CreateClient();
            factory.SetZgwToken(client);

            using var response = await client.GetAsync($"{BaseUrl}?type={factory.INTERNE_TAAK_OBJECT_TYPE_URL}{queryIn}");
            Assert.True(response.IsSuccessStatusCode);
        }
    }
}
