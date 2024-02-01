namespace PodiumdAdapter.Web.Test
{
    public class GetZakenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Intern_zaaknummer_overschrijft_identificatie_indien_beschikbaar()
        {
            const string EsuiteResponse = """
            {"results":[{"internZaaknummer":"12345","identificatie":"54321"}]}
            """;

            const string ExpectedApiResponse = """
            {"results":[{"internZaaknummer":"12345","identificatie":"12345"}]}
            """;

            using var client = factory.CreateClient();
            factory.Login(client);
            factory.MockHttpMessageHandler
                .Expect(HttpMethod.Get, factory.ESUITE_BASE_URL + "/zgw-apis-provider/zrc/api/v1/zaken")
                .Respond("application/json", EsuiteResponse);

            var response = await client.GetStringAsync("/zaken/api/v1/zaken");

            Assert.Equal(ExpectedApiResponse, response);
        }

        [Fact]
        public async Task Identificatie_blijft_gelijk_als_intern_zaaknummer_ontbreekt()
        {
            const string EsuiteResponse = """
            {"results":[{"identificatie":"12345"}]}
            """;

            const string ExpectedApiResponse = """
            {"results":[{"identificatie":"12345"}]}
            """;

            using var client = factory.CreateClient();
            factory.Login(client);
            factory.MockHttpMessageHandler
                .Expect(HttpMethod.Get, factory.ESUITE_BASE_URL + "/zgw-apis-provider/zrc/api/v1/zaken")
                .Respond("application/json", EsuiteResponse);

            var response = await client.GetStringAsync("/zaken/api/v1/zaken");

            Assert.Equal(ExpectedApiResponse, response);
        }
    }
}
