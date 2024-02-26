namespace PodiumdAdapter.Web.Test;

// TODO de naam van de test class dekt de lading niet en uit de code is niet goed af te lezen wat er hoet en waarom getest wordt.
public class KlantenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Get_klanten_request_is_forwarded_to_expected_Url()
    {

        var esuiteUrl = factory.ESUITE_BASE_URL + "/klanten-api-provider/api/v1/klanten";
        var adapterPath = "/klanten/api/v1/klanten";

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
}
