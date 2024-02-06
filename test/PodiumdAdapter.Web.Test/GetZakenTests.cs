namespace PodiumdAdapter.Web.Test
{
    public class GetZakenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {


        [Fact]
        public async Task Get_zaken_request_is_forwarded_to_expected_Url()
        {
            
            var esuiteZakenUrl = factory.ESUITE_BASE_URL + "/zgw-apis-provider/zrc/api/v1/zaken";
            var adapterZakenPath = "/zaken/api/v1/zaken";

            using var client = factory.CreateClient();
            factory.Login(client);

            //MockHttpMessageHandler will intercept calls from the Adapter to e-Suite
            //If the adapter behaves as expected
            //esuiteZakenUrl will be called from the adapter
            //if a call to the adapter is made on the adapterZakenUrl

            var requestsToEsuiteZakenUrl = factory.MockHttpMessageHandler
                .Expect(HttpMethod.Get, esuiteZakenUrl)
                .Respond("application/json", "{}");
       
            //call the adapter with the zaken path
            await client.GetAsync(adapterZakenPath);
            
            var nrOfCallsToEsuiteZakenUrl = factory.MockHttpMessageHandler.GetMatchCount(requestsToEsuiteZakenUrl);

            Assert.Equal(1, nrOfCallsToEsuiteZakenUrl);

        }


        [Fact]
        public async Task Intern_zaaknummer_overschrijft_identificatie_indien_beschikbaar()
        {
            const string EsuiteResponse = """
            {"results":[{"identificatieIntern":"12345","identificatie":"54321"}]}
            """;

            const string ExpectedApiResponse = """
            {"results":[{"identificatieIntern":"12345","identificatie":"12345"}]}
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
