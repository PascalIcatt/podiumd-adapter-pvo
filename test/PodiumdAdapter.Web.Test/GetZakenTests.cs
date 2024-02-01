namespace PodiumdAdapter.Web.Test
{
    public class GetZakenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Intern_zaaknummer_wordt_toegevoegd_als_identificatie()
        {
            const string EsuiteResponse = """
            {"results":[{"internZaaknummer":"12345"}]}
            """;

            const string ExpectedApiResponse = """
            {"results":[{"internZaaknummer":"12345","identificatie":"12345"}]}
            """;

            using var client = factory.CreateClient();
            factory.Login(client);
            factory.MockHttpMessageHandler
                .Expect(HttpMethod.Get, "https://localhost:12345/zgw-apis-provider/zrc/api/v1/zaken")
                .Respond("application/json", EsuiteResponse);

            var response = await client.GetStringAsync("/zaken/api/v1/zaken");

            Assert.Equal(ExpectedApiResponse, response);
        }
    }
}
