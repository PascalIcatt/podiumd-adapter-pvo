using System.Net.Http.Json;

namespace PodiumdAdapter.Web.Test
{
    public class InterneTaakTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        const string baseUrl = "/api/v2/objects";

        [Fact]
        public async Task Post_response_contains_correct_url()
        {
            var cmId = Guid.NewGuid().ToString();
            var cmUrl = "https://www.google.nl/" + cmId;
            var expectedContent = $$"""
            {"url":"http://localhost{{baseUrl}}/{{cmId}}"}
            """;

            using var client = factory.CreateClient();
            factory.SetObjectenToken(client);

            using var response = await client.PostAsJsonAsync(baseUrl, new
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
                {"results":[{"afdeling":"Algemeen","behandelaar":{"gebruikersnaam":"Gebruikersnaam","volledigeNaam":"Voornaam Achternaam","toelichting":"toelichting"},"bronorganisatie":"CHANGEME","contactgegevens":{"emailadres":"admin@example.com","telefoonnummer":"0612345678"},"identificatie":"73-2024","kanaal":"contactformulier","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"},"onderwerp":"afd:Algemeen","registratiedatum":"2024-02-01T12:21:35+01:00","status":"nieuw","tekst":"vraag","type":"Terugbelverzoek","url":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","objectcontactmomenten":[]}]}
                """;

            const string ExpectedResult = """
                {"results":[{"url":"http://localhost/api/v2/objects/adc06246-4143-449e-a79a-3623442b24a0","uuid":"adc06246-4143-449e-a79a-3623442b24a0","type":"mytype","record":{"index":1,"typeVersion":1,"data":{"actor":{"naam":"Voornaam Achternaam","soortActor":"medewerker","identificatie":"Gebruikersnaam"},"status":"nieuw","betrokkene":{"rol":"klant","klant":null,"digitaleAdressen":[{"adres":"admin@example.com","omschrijving":"e-mailadres","soortDigitaalAdres":"e-mailadres"},{"adres":"0612345678","omschrijving":"telefoonnummer","soortDigitaalAdres":"telefoonnummer"}]},"toelichting":"toelichting","contactmoment":"http://localhost:56090/contactmomenten/api/v1/contactmomenten/adc06246-4143-449e-a79a-3623442b24a0","registratiedatum":"2024-02-01T12:21:35+01:00","medewerkerIdentificatie":{"identificatie":"Gebruikersnaam","achternaam":"Voornaam Achternaam"}}}}]}
                """;

            factory.MockHttpMessageHandler
                .When("*")
                .Respond("application/json", ApiResponse);

            using var client = factory.CreateClient();
            factory.SetObjectenToken(client);

            var str = await client.GetStringAsync(baseUrl + "?type=mytype");
            Assert.Equal(ExpectedResult, str);
        }
    }
}
