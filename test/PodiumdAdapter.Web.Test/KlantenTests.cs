using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web.Test;

public class KlantenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    const string KlantenBaseUrl = "/klanten/api/v1";

    [Fact]
    public async Task Proxy_returns_correct_response_when_logged_in()
    {
        const string OriginalInput = "{'replace me' : 'http://localhost:0/zaken/api/v1/'}";
        const string Replaced = "{'replace me' : 'https://localhost:12345/zgw-apis-provider/zrc/api/v1/'}";
        
        var path = "/klanten/" + Guid.NewGuid(); ;
        using var client = factory.CreateClient();
        factory.Login(client);

        factory.MockHttpMessageHandler
            .Expect(HttpMethod.Patch, factory.ESUITE_BASE_URL + "/klanten-api-provider/api/v1" + path)
            .Respond("application/json", Replaced);

        using var content = new StringContent(OriginalInput, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
        using var response = await client.PatchAsync(KlantenBaseUrl + path, content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(OriginalInput, body);
    }
}
