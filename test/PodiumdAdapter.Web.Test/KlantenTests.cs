using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web.Test;

// TODO de naam van de test class dekt de lading niet en uit de code is niet goed af te lezen wat er hoet en waarom getest wordt.
public class KlantenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    const string KlantenBaseUrl = "/klanten/api/v1";

    [Fact]
    public async Task Integration_test_for_url_rewriting_request_and_response_bodies()
    {
        const string OriginalInput = "{'replace me' : 'http://localhost:0/zaken/api/v1/'}";
        const string Replaced = "{'replace me' : 'https://localhost:12345/zgw-apis-provider/zrc/api/v1/'}";
        
        var path = "/klanten/" + Guid.NewGuid(); ;
        using var client = factory.CreateClient();
        factory.Login(client);

        var request = factory.MockHttpMessageHandler
            .Expect(HttpMethod.Patch, factory.ESUITE_BASE_URL + "/klanten-api-provider/api/v1" + path)
            .WithContent(Replaced)
            .Respond("application/json", Replaced);

        using var content = new StringContent(OriginalInput, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
        using var response = await client.PatchAsync(KlantenBaseUrl + path, content);
        var body = await response.Content.ReadAsStringAsync();

        var timesTheMockHttpMessageHandlerWasTriggered = factory.MockHttpMessageHandler.GetMatchCount(request);
        Assert.Equal(1, timesTheMockHttpMessageHandlerWasTriggered);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(OriginalInput, body);
    }
}
