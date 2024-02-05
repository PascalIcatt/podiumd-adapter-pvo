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

}
